using MesWorklog.Data;
using MesWorklog.Dtos;
using MesWorklog.Exceptions;
using MesWorklog.Models;
using Microsoft.EntityFrameworkCore;

namespace MesWorklog.Services
{

    // C#에서 Service는 ASP.NET Core는 Program.cs에 직접 등록해야 함

    // ToListAsync() — 여러 건 조회, 리스트로 (SELECT ... WHERE ...)
    // FirstOrDefaultAsync() — 단건 조회, 없으면 null (SELECT ... LIMIT 1)
    // AnyAsync(조건) — 존재 여부만 (true/false) (SELECT EXISTS...)
    // CountAsync(조건) — 건수 (SELECT COUNT(*)  ...
    // FindAsync(PK) - 캐시먼저 확인, PK 조회 (SELECT ... WHERE PK = ?)
    // SumAsync(선택자) - 합계 계산(SELECT SUM()... WHERE ...)
    // SaveChangesAsync() -  한 트랜잭션으로 INSERT/UPDATE/DELETE 실행. 중간에 실패시 Rollback (INSERT, UPDATE, DELETE...)
    public class EquipmentService
    {
        // 보관할 필드 생성자에서만 대입가능, 이 후 변경불가
        private readonly AppDbContext _db;

        // 생성자
        // 요청이오면 DI컨테이너가 만들어서 보관
        public EquipmentService(AppDbContext db)
        {
            // 필드에 보관
            _db = db;
        }

        // 설비목록 조회 
        // includeInactive=false면 활성 설비만 조회 true면 비활성까지 조회
        // 현재 웹에서는 비활성 설비 조회기능은없음
        public async Task<List<EquipmentResponse>> GetAllAsync(bool includeInactive)
        {
            // EFCore는 기본적으로 조회한 객체의 원래 값 스냅샷을 보관함
            // 조회 전용이라 변경 추적이 불필요
            // AsNoTracking: 스냅샷을 안 만들어 더 가벼움
            var query = _db.Equipments.AsNoTracking();

            if (!includeInactive)
                query = query.Where(l => l.IsActive);

            // Select로 필요한 컬럼만 뽑아 DTO로 변환.
            // 이렇게 하면 SQL도 SELECT id, name, is_active 만 나가서 불필요한 컬럼을 안 읽음
            return await query
                .OrderBy(l => l.Name)
                .Select(l => new EquipmentResponse(l.Id, l.Name, l.IsActive))
                .ToListAsync();
        }

        // 단건조회
        public async Task<EquipmentResponse> GetByIdAsync(int id)
        {

            var equipment = await _db.Equipments.AsNoTracking()
                .Where(l => l.Id == id)
                .Select(l => new EquipmentResponse(l.Id, l.Name, l.IsActive))
                .FirstOrDefaultAsync();

            // ?? 는 왼쪽이 null이면 오른쪽을 실행(null 병합 연산자)
            // KeyNotFoundException은 미들웨어가 404로 변환한다
            return equipment ?? throw new KeyNotFoundException($"설비({id})를 찾을 수 없습니다.");
        }

        // 설비등록
        public async Task<EquipmentResponse> CreateAsync(CreateEquipmentRequest request)
        {
            // 이름 중복 검사. DB 유니크 제약을 안 걸었으므로 여기서만 제약
            // AnyAsync : 하나라도 있는지 확인, SELECT EXISTS... 로 하나만 찾으면 멈춤
            var duplicated = await _db.Equipments.AnyAsync(l => l.Name == request.Name);
            if (duplicated)
                throw new BusinessRuleException($"이미 존재하는 설비명입니다: {request.Name}");

            var equipment = new Equipment { Name = request.Name };

            _db.Equipments.Add(equipment);          // 이 시점엔 아직 DB에 안 나감
            await _db.SaveChangesAsync(); // 여기서 INSERT 실행되고, equipment.Id에 생성된 값이 채워짐

            return new EquipmentResponse(equipment.Id, equipment.Name, equipment.IsActive);
        }

        // 설비수정
        public async Task<EquipmentResponse> UpdateAsync(int id, UpdateEquipmentRequest request)
        {
            // 여기는 AsNoTracking을 쓰면 안 됨 — 추적해야 SaveChanges가 변경분을 감지함
            // FindAsync : PK로 찾는 전용매서드, 항상 추적, 이미 추적 중인 객체가 있다면 DB에는 가지 않음
            var equipment = await _db.Equipments.FindAsync(id)
                ?? throw new KeyNotFoundException($"설비({id})를 찾을 수 없습니다.");

            // 자기 자신은 제외하고 중복 검사
            var duplicated = await _db.Equipments.AnyAsync(l => l.Name == request.Name && l.Id != id);
            if (duplicated)
                throw new BusinessRuleException($"이미 존재하는 설비명입니다: {request.Name}");


            // 여기서는 프로퍼티만 바꿔두면 SaveChanges가 변경된 컬럼만 골라 UPDATE를 만들어냄
            equipment.Name = request.Name;
            equipment.IsActive = request.IsActive;

            await _db.SaveChangesAsync();

            return new EquipmentResponse(equipment.Id, equipment.Name, equipment.IsActive);
        }

        // 설비삭제
        // 작업 이력이 걸려 있으면 비활성화, 없으면 실제 삭제
        public async Task<DeleteResult> DeleteAsync(int id)
        {
            var equipment = await _db.Equipments.FindAsync(id)
                ?? throw new KeyNotFoundException($"설비({id})를 찾을 수 없습니다.");

            // CountAsync : 응답에 건수를 담을 수 있음
            // "작업 이력 120건이 있어 비활성 처리했습니다" 같은 안내를 하기 위함
            var historyCount = await _db.WorkLogs.CountAsync(o => o.EquipmentId == id);

            if (historyCount > 0)
            {
                equipment.IsActive = false;
                await _db.SaveChangesAsync();
                return new DeleteResult("deactivated", historyCount);
            }

            // 이력이 없을 때만 실제 삭제.
            _db.Equipments.Remove(equipment);
            await _db.SaveChangesAsync();
            return new DeleteResult("deleted");
        }
    }
}
