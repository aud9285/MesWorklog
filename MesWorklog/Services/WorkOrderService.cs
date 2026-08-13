using MesWorklog.Common;
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
    public class WorkOrderService
    {
        // 보관할 필드 생성자에서만 대입가능, 이 후 변경불가
        private readonly AppDbContext _db;

        // 생성자
        // 요청이오면 DI컨테이너가 만들어서 보관
        public WorkOrderService(AppDbContext db)
        {
            _db = db;
        }

        // 이어하기 목록
        // 작업지시서(WokrOrder)기준 목표수량에 해당 작업지시(WorkOrder)의 작업이력(WorkLog)의 완료수량 합이 도달하지 않은 목록
        // + 작업자 기준 배정된 공정의 작업지시만
        public async Task<List<OpenWorkOrderResponse>> GetOpenByWorkerAsync(int workerId)
        {
            // 이 작업자가 배정된 공정 id들 (WorkerProcess 조인)
            var myProcessIds = await _db.WorkerProcesses
                .Where(wp => wp.WorkerId == workerId)
                .Select(wp => wp.ProcessId)
                .ToListAsync();

            // 배정된 공정이 없으면 이어할 대상도 없다 — DB에 더 안 가고 바로 반환
            if (myProcessIds.Count == 0)
                return new List<OpenWorkOrderResponse>();

            var orders = await _db.WorkOrders
                .AsNoTracking()
                .Include(o => o.Line)
                .Include(o => o.Process)
                .Include(o => o.WorkLogs)          // 누적 실적/인원 수를 세는 데 필요
                // CompletedAt이 null = 목표 미달 = 아직 이어할 수 있음
                .Where(o => o.CompletedAt == null && myProcessIds.Contains(o.ProcessId))
                .ToListAsync();

            // 집계는 메모리에서 — WorkLogs를 이미 Include로 가져왔으므로 추가 쿼리가 없다
            return orders.Select(o => new OpenWorkOrderResponse(
                o.Id,
                o.LineId, o.Line.Name,
                o.ProcessId, o.Process.Name,
                o.TargetQty,
                // 누적실적 = 완료된 이력들의 수량 합
                o.WorkLogs.Where(w => w.Status == WorkLogStatus.Completed).Sum(w => w.ActualQty),    
                // 진행중 인원 체크
                o.WorkLogs.Count(w => w.Status == WorkLogStatus.InProgress
                                   || w.Status == WorkLogStatus.Paused)))
                .ToList();
        }

        // 작업지시 수정 — 라인/공정 오선택 정정, 목표수량 조정
        // 목표수량을 실적 이하로 낮추면 그 자리에서 완료 처리
        // (설비 고장으로 남은 수량을 새 작업지시로 이어갈 때, 기존 건은 여기서 마감하는 흐름)
        public async Task<WorkOrderResponse> UpdateAsync(int id, UpdateWorkOrderRequest request)
        {
            var workOrder = await _db.WorkOrders
                .Include(o => o.Line)
                .Include(o => o.Process)
                .FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new KeyNotFoundException($"작업지시({id})를 찾을 수 없습니다.");

            // 완료된 작업지시는 잠금 — 고치려면 이력을 삭제하고 다시 작성해야 함
            if (workOrder.CompletedAt != null)
                throw new BusinessRuleException("이미 완료된 작업지시는 수정할 수 없습니다.");

            // 라인-공정 조합 검증 — StartAsync 신규 생성 때와 동일한 규칙
            var isValidCombo = await _db.LineProcesses
                .AnyAsync(lp => lp.LineId == request.LineId && lp.ProcessId == request.ProcessId);
            if (!isValidCombo)
                throw new BusinessRuleException("해당 라인에서 이 공정은 진행할 수 없는 조합입니다.");

            // 누적 실적 — 자동완료 판정 기준
            var completedQty = await _db.WorkLogs
                .Where(w => w.WorkOrderId == id && w.Status == WorkLogStatus.Completed)
                .SumAsync(w => w.ActualQty);

            // 목표수량을 실적 이하로 낮추는 요청인지
            var willAutoComplete = request.TargetQty <= completedQty;

            if (willAutoComplete)
            {
                // 아직 진행중/정지중인 이력이 남아있으면 자동완료 금지
                // (고장 → 재개 → 마감 을 먼저 끝낸 뒤에 수량을 정정하라는 순서 강제)
                var hasActiveSession = await _db.WorkLogs.AnyAsync(w =>
                    w.WorkOrderId == id &&
                    (w.Status == WorkLogStatus.InProgress || w.Status == WorkLogStatus.Paused));

                if (hasActiveSession)
                    throw new BusinessRuleException(
                        "진행 중이거나 정지 중인 작업이 있어 완료 처리할 수 없습니다. 먼저 재개 후 종료해주세요.");
            }

            workOrder.LineId = request.LineId;
            workOrder.ProcessId = request.ProcessId;
            workOrder.TargetQty = request.TargetQty;

            if (willAutoComplete)
                workOrder.CompletedAt = KoreaTime.Now;

            await _db.SaveChangesAsync();

            // Line/Process를 새 값으로 바꿨으므로, 응답용 이름은 다시 조회해서 내려줌
            var line = await _db.Lines.FirstAsync(l => l.Id == workOrder.LineId);
            var process = await _db.Processes.FirstAsync(p => p.Id == workOrder.ProcessId);

            return new WorkOrderResponse(
                workOrder.Id,
                workOrder.LineId, line.Name,
                workOrder.ProcessId, process.Name,
                workOrder.TargetQty,
                completedQty,
                workOrder.CompletedAt);
        }

    }


}