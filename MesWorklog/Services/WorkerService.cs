using MesWorklog.Common;
using MesWorklog.Data;
using MesWorklog.Dtos;
using MesWorklog.Exceptions;
using MesWorklog.Models;
using Microsoft.EntityFrameworkCore;

namespace MesWorklog.Services
{
    
    // C#에서 Service는 ASP.NET Core는 Program.cs에 직접 등록해야 함
    public class WorkerService
    {
        // 보관할 필드 생성자에서만 대입가능, 이 후 변경불가
        private readonly AppDbContext _db;

        // 생성자
        // 요청이오면 DI컨테이너가 만들어서 보관
        public WorkerService(AppDbContext db)
        {
            // 필드에 보관
            _db = db;
        }

        // 작업자목록 조회 
        // includeInactive=false면 활성 작업자만 조회 true면 비활성까지 조회
        // 현재 웹에서는 비활성 작업자 조회기능은없음
        public async Task<List<WorkerResponse>> GetAllAsync(bool includeInactive)
        {
            // EFCore는 기본적으로 조회한 객체의 원래 값 스냅샷을 보관함
            // 조회 전용이라 변경 추적이 불필요
            // AsNoTracking: 스냅샷을 안 만들어 더 가벼움
            var query = _db.Workers.AsNoTracking();

            if (!includeInactive)
                query = query.Where(l => l.IsActive);

            // Select로 필요한 컬럼만 뽑아 DTO로 변환.
            // 이렇게 하면 SQL도 SELECT id, name, is_active 만 나가서 불필요한 컬럼을 안 읽음
            return await query
                .OrderBy(l => l.Name)
                .Select(l => new WorkerResponse(l.Id, l.Name, l.IsActive,
                l.WorkerProcesses.Select(lp => lp.ProcessId).ToList()))
                .ToListAsync();
        }

        // 단건조회
        public async Task<WorkerResponse> GetByIdAsync(int id)
        {

            var worker = await _db.Workers.AsNoTracking()
                .Where(l => l.Id == id)
                .Select(l => new WorkerResponse(l.Id, l.Name, l.IsActive,
                 l.WorkerProcesses.Select(lp => lp.ProcessId).ToList()))
                .FirstOrDefaultAsync();

            // ?? 는 왼쪽이 null이면 오른쪽을 실행(null 병합 연산자)
            // KeyNotFoundException은 미들웨어가 404로 변환한다
            return worker ?? throw new KeyNotFoundException($"작업자({id})를 찾을 수 없습니다.");
        }

        // 작업자등록
        public async Task<WorkerResponse> CreateAsync(CreateWorkerRequest request)
        {
            // 이름 중복 검사. DB 유니크 제약을 안 걸었으므로 여기서만 제약
            // AnyAsync : 하나라도 있는지 확인, SELECT EXISTS... 로 하나만 찾으면 멈춤

            // 동명이인 case 때문에 주석처리
            //var duplicated = await _db.Workers.AnyAsync(l => l.Name == request.Name);
            //if (duplicated)
            //    throw new BusinessRuleException($"이미 존재하는 작업자명입니다: {request.Name}");

            var worker = new Worker { Name = request.Name };

            // 요청에 담긴 공정 id 목록
            // WorkerProcess에 공정 id 추가
            // 작업자 id는 안채움 작업자 id 생성전이라 없음 네비게이션에 넣어두면 SaveChangesAsync()떄 EFCore에서 id 채워줌
            foreach (var processid in request.ProcessIds)
            {
                worker.WorkerProcesses.Add(new WorkerProcess{ ProcessId= processid });
            }


            _db.Workers.Add(worker);          // 이 시점엔 아직 DB에 안 나감
            await _db.SaveChangesAsync(); // 여기서 INSERT 실행되고, worker.Id에 생성된 값이 채워짐

            return new WorkerResponse(worker.Id, worker.Name, worker.IsActive, request.ProcessIds);
        }

        // 작업자수정
        public async Task<WorkerResponse> UpdateAsync(int id, UpdateWorkerRequest request)
        {
            // 여기는 AsNoTracking을 쓰면 안 됨 — 추적해야 SaveChanges가 변경분을 감지함
            // FindAsync : PK로 찾는 전용매서드, 항상 추적, 이미 추적 중인 객체가 있다면 DB에는 가지 않음
            var worker = await _db.Workers.FindAsync(id)
                ?? throw new KeyNotFoundException($"작업자({id})를 찾을 수 없습니다.");

            // 자기 자신은 제외하고 중복 검사

            // 동명이인 case 때문에 주석처리
            //var duplicated = await _db.Workers.AnyAsync(l => l.Name == request.Name && l.Id != id);
            //if (duplicated)
            //    throw new BusinessRuleException($"이미 존재하는 작업자명입니다: {request.Name}");


            // 여기서는 프로퍼티만 바꿔두면 SaveChanges가 변경된 컬럼만 골라 UPDATE를 만들어냄
            worker.Name = request.Name;
            worker.IsActive = request.IsActive;

            // 이 작업자에 지금 연결된 공정 id들을 DB에서 조회 (Diff의 existingIds)
            var existingProcessIds = await _db.WorkerProcesses
                .Where(lp => lp.WorkerId == id)
                .Select(lp => lp.ProcessId)
                .ToListAsync();

            // JoinTableSync에서 insert할지 delete 할지 구분 — DB 접근 없음
            var (toInsert, toDelete) = JoinTableSync.Diff(existingProcessIds, request.ProcessIds);

            // 구분한 결과대로 실제 추가/삭제
            foreach (var processId in toInsert)
                _db.WorkerProcesses.Add(new WorkerProcess { ProcessId = processId, WorkerId = id });

            foreach (var processId in toDelete)
            {
                var row = await _db.WorkerProcesses.FindAsync(id, processId);
                if (row != null) _db.WorkerProcesses.Remove(row);
            }


            await _db.SaveChangesAsync();

            return new WorkerResponse(worker.Id, worker.Name, worker.IsActive, request.ProcessIds);
        }

        // 작업자삭제
        // 작업 이력이 걸려 있으면 비활성화, 없으면 실제 삭제
        public async Task<DeleteResult> DeleteAsync(int id)
        {
            var worker = await _db.Workers.FindAsync(id)
                ?? throw new KeyNotFoundException($"작업자({id})를 찾을 수 없습니다.");

            // CountAsync : 응답에 건수를 담을 수 있음
            // "작업 이력 120건이 있어 비활성 처리했습니다" 같은 안내를 하기 위함
            var historyCount = await _db.WorkLogs.CountAsync(o => o.WorkerId == id);

            if (historyCount > 0)
            {
                worker.IsActive = false;
                await _db.SaveChangesAsync();
                return new DeleteResult("deactivated", historyCount);
            }

            // 이력이 없을 때만 실제 삭제. WorkerProcess 행들은 DB의 Cascade로 함께 사라짐
            _db.Workers.Remove(worker);
            await _db.SaveChangesAsync();
            return new DeleteResult("deleted");
        }
    }
}
