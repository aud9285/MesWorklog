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
    public class WorkLogService
    {
        // 보관할 필드 생성자에서만 대입가능, 이 후 변경불가
        private readonly AppDbContext _db;

        // 생성자
        // 요청이오면 DI컨테이너가 만들어서 보관
        public WorkLogService(AppDbContext db)
        {
            _db = db;
        }

        // 작업 시작 — 이어하기(기존 작업지시) 또는 신규 작업지시 생성 두 경로를 하나로 처리
        public async Task<WorkLogResponse> StartAsync(CreateWorkLogRequest request)
        {
            // 작업자 중복 시작 방지 — 이미 진행중/정지중인 건이 있으면 새로 시작 불가
            /*
             * SELECT EXISTS (
                    SELECT 1 FROM work_logs
                    WHERE worker_id = ? AND status IN ('InProgress', 'Paused')
                )
             */
            var hasActive = await _db.WorkLogs.AnyAsync(w =>
                w.WorkerId == request.WorkerId &&
                (w.Status == WorkLogStatus.InProgress || w.Status == WorkLogStatus.Paused));

            if (hasActive)
                throw new BusinessRuleException("이미 진행 중인 작업이 있어 새로 시작할 수 없습니다.");

            // 작업자 시간 겹침 방지
            // 완료된 이력에서 겹치는 시간이 있는지 체크
            // 작업시작시간 >= 완료된건의 시작시간 + 작업시작시간 <= 완료된건의 종료시간일때 에러처리
            var conflicting = await _db.WorkLogs
              .Where(w => w.WorkerId == request.WorkerId
                       && w.Status == WorkLogStatus.Completed
                       && request.StartTime >= w.StartTime
                       && request.StartTime < w.EndTime)
              .FirstOrDefaultAsync();

            if (conflicting != null)
                throw new BusinessRuleException(
                    $"{conflicting.StartTime:yyyy-MM-dd HH:mm}~{conflicting.EndTime:HH:mm}에 이미 종료된 작업이 있습니다.");

            int workOrderId;

            if (request.WorkOrderId.HasValue)
            {
                // 이어하기 — 그 작업지시가 실제로 존재하는지만 확인
                var exists = await _db.WorkOrders.AnyAsync(o => o.Id == request.WorkOrderId.Value);
                if (!exists)
                    throw new KeyNotFoundException($"작업지시({request.WorkOrderId})를 찾을 수 없습니다.");

                workOrderId = request.WorkOrderId.Value;
            }
            else
            {
                // 신규 생성 — 라인/공정/목표수량은 필수
                if (request.LineId is null || request.ProcessId is null || request.TargetQty is null)
                    throw new BusinessRuleException("신규 작업지시 생성에는 라인, 공정, 목표수량이 필요합니다.");

                // 라인-공정 조합 검증 — 프론트는 캐스케이딩 셀렉트로 이미 걸러주지만,
                // Swagger 직접 호출이나 오래된 캐시 화면은 이 검증을 우회할 수 있어 서버에서 다시 확인
                var isValidCombo = await _db.LineProcesses
                    .AnyAsync(lp => lp.LineId == request.LineId && lp.ProcessId == request.ProcessId);

                if (!isValidCombo)
                    throw new BusinessRuleException("해당 라인에서 이 공정은 진행할 수 없는 조합입니다.");

                var workOrder = new WorkOrder
                {
                    LineId = request.LineId.Value,
                    ProcessId = request.ProcessId.Value,
                    TargetQty = request.TargetQty.Value,
                };

                _db.WorkOrders.Add(workOrder);
                await _db.SaveChangesAsync();   // WorkLog.Start가 int workOrderId를 요구해서, Id를 먼저 확정해야 함

                workOrderId = workOrder.Id;
            }

            // 상태 전이/시간 검증은 엔티티가 스스로 책임짐 (서비스는 조립만)
            var log = WorkLog.Start(workOrderId, request.WorkerId, request.EquipmentId, request.StartTime, now: KoreaTime.Now);

            _db.WorkLogs.Add(log);
            await _db.SaveChangesAsync();

            return ToResponse(log);

        }

        // 작업 정지 - 진행중 상태의 작업이력만 가능 (실제 가드는 WorkLog.pause 내부에 있음)
        public async Task<WorkLogResponse> PauseAsync(int id, PauseWorkLogRequest request)
        {
            // WorkLog 조회 (Pauses까지 같이 로딩 필요 — Pause() 내부가 lastResumedAt 계산 시 Pauses를 읽음)
            var workLog = await _db.WorkLogs
                .Include(w => w.Pauses)
                .FirstOrDefaultAsync(w => w.Id == id)
                ?? throw new KeyNotFoundException($"작업이력({id})을 찾을 수 없습니다.");

            // PauseReason 실제 조회 — 새 시그니처가 엔티티를 요구하므로 여기서 미리 확보
            var pauseReason = await _db.PauseReasons.FindAsync(request.PauseReasonId)
                ?? throw new KeyNotFoundException($"정지사유({request.PauseReasonId})를 찾을 수 없습니다.");

            // 도메인 로직 호출 — 상태 전이/시간 검증은 엔티티가 책임짐
            workLog.Pause(request.PausedAt, pauseReason, KoreaTime.Now);

            await _db.SaveChangesAsync();

            return ToResponse(workLog);
        }


        // 작업 재개 — 정지중 상태의 작업이력만 가능 (실제 가드는 WorkLog.Resume 내부에 있음)
        public async Task<WorkLogResponse> ResumeAsync(int id, ResumeWorkLogRequest request)
        {
            // Pauses까지 로딩 — Resume() 내부가 "재개 되지않은 정지 이력(ResumedAt == null)"을 Pauses에서 찾기 때문
            var workLog = await _db.WorkLogs
                .Include(w => w.Pauses)
                .FirstOrDefaultAsync(w => w.Id == id)
                ?? throw new KeyNotFoundException($"작업이력({id})을 찾을 수 없습니다.");

            workLog.Resume(request.ResumedAt, KoreaTime.Now);

            await _db.SaveChangesAsync();

            return ToResponse(workLog);
        }


        // 작업 완료 — 진행중 상태의 작업이력만 가능. 조업/가동/실가동 시간이 여기서 확정 계산됨
        public async Task<WorkLogResponse> CompleteAsync(int id, CompleteWorkLogRequest request)
        {
            // Pauses + PauseReason까지 로딩 — Complete()가 p.PauseReason.Category를 읽어서
            // 계획정지/비가동을 구분하므로, ThenInclude를 빠뜨리면 그 자리에서 NullReferenceException
            var workLog = await _db.WorkLogs
                .Include(w => w.Pauses)
                    .ThenInclude(p => p.PauseReason)
                .Include(w => w.WorkOrder)
                .FirstOrDefaultAsync(w => w.Id == id)
                ?? throw new KeyNotFoundException($"작업이력({id})을 찾을 수 없습니다.");

            // 완료시 완료건들중에 겹치는 시간이 있는지 체크
            // 작업건 시작시간 < 완료건 종료시간 && 작업건 종료시간 > 완료건 시작시간시 에러발생

            // 완료건시작시간 비교 ex
            // 12:00 ~ 15:00(완료건) 09:00 ~ 12:00(작업건) 정상 case
            // 09:00 < 15:00 and 12:00 > 12:00 false 통과

            // 12:00 ~ 15:00(완료건) 15:00 ~ 18:00(작업건) 정상 case
            // 15:00 < 15:00 and 18:00 > 12:00 false 통과

            // 12:00 ~ 15:00(완료건) 09:00 ~ 13:00(작업건) 비정상 case
            // 09:00 < 15:00 and 13:00 > 12:00 true 에러발생

            // 12:00 ~ 15:00(완료건) 09:00 ~ 18:00(작업건) 비정상 case
            // 09:00 < 15:00 and 18:00 > 12:00 true 에러발생
            var overlapping = await _db.WorkLogs
                .Where(w => w.WorkerId == workLog.WorkerId
                         && w.Status == WorkLogStatus.Completed
                         && w.Id != workLog.Id
                         && workLog.StartTime < w.EndTime      
                         && w.StartTime < request.EndTime)     
                .FirstOrDefaultAsync();

            if (overlapping != null)
                throw new BusinessRuleException(
                    $"{overlapping.StartTime:yyyy-MM-dd HH:mm}~{overlapping.EndTime:HH:mm}에 이미 종료된 작업과 시간이 겹칩니다.");

            workLog.Complete(request.EndTime, request.ActualQty, KoreaTime.Now);


            // 이 작업지시의 누적 수량이 목표 수량에 도달했는지 체크
            var otherCompletedQty = await _db.WorkLogs
                .Where(w => w.WorkOrderId == workLog.WorkOrderId
                         && w.Id != workLog.Id
                         && w.Status == WorkLogStatus.Completed)
                .SumAsync(w => w.ActualQty);

            // 목표에 도달 시에만 완료 시각을 찍는다. 이미 찍혀 있으면 그대로 둔다(먼저 도달한 시점이 맞음)
            if (otherCompletedQty + request.ActualQty >= workLog.WorkOrder.TargetQty)
                workLog.WorkOrder.CompletedAt ??= KoreaTime.Now;


            await _db.SaveChangesAsync();

            return ToResponse(workLog);
        }


        // 오입력이나 종료를 잊고 방치된 이력을 삭제한다
        // 이력을 지우면 그 작업지시의 누적 실적이 줄어들 수 있어, 완료 판정을 다시 매긴다
        public async Task<DeleteResult> DeleteAsync(int id)
        {
            var workLog = await _db.WorkLogs
                .Include(w => w.WorkOrder)
                .FirstOrDefaultAsync(w => w.Id == id)
                ?? throw new KeyNotFoundException($"작업이력({id})을 찾을 수 없습니다.");

            var workOrder = workLog.WorkOrder;

            // 같은 작업지시에 붙어 있는 다른 이력 수 (자기 자신 제외)
            var siblingCount = await _db.WorkLogs
                .CountAsync(w => w.WorkOrderId == workLog.WorkOrderId && w.Id != id);

            // WorkLogPause 행들은 DB Cascade로 함께 사라진다
            _db.WorkLogs.Remove(workLog);

            if (siblingCount == 0)
            {
                // 마지막 이력이면 작업지시도 함께 삭제한다.
                // WorkLog 0건인 WorkOrder는 시작 시점에 함께 생성되는 구조상 존재할 수 없고(§4-4),
                // 남겨두면 실적 없는 껍데기가 이어하기 목록에 계속 뜬다
                _db.WorkOrders.Remove(workOrder);
                await _db.SaveChangesAsync();
                return new DeleteResult("deleted_with_order");
            }

            // 남은 이력들의 누적 실적을 다시 계산한다.
            // 삭제 대상은 아직 SaveChanges 전이라 DB에 남아 있으므로 w.Id != id로 직접 제외해야 한다
            var remainingQty = await _db.WorkLogs
                .Where(w => w.WorkOrderId == workLog.WorkOrderId
                         && w.Id != id
                         && w.Status == WorkLogStatus.Completed)
                .SumAsync(w => w.ActualQty);

            if (remainingQty >= workOrder.TargetQty)
            {
                // 여전히 목표 달성 상태 — 기존 완료 시각을 유지한다
                workOrder.CompletedAt ??= KoreaTime.Now;
            }
            else
            {
                // 목표 미달로 떨어졌다 → 다시 이어하기 대상이 되어야 한다
                workOrder.CompletedAt = null;
            }

            await _db.SaveChangesAsync();
            return new DeleteResult("deleted", siblingCount);
        }

        // 그 작업자의 활성(진행중/정지중) 건 1개. 없으면 null을 반환한다.
        // 한 작업자당 활성 건은 최대 1개라는 규칙(StartAsync의 중복 시작 방지)이 전제다
        public async Task<ActiveWorkLogResponse?> GetActiveByWorkerAsync(int workerId)
        {
            // 조회 전용이라 AsNoTracking — 변경 추적 스냅샷을 안 만들어 더 가볍다
            // 화면에ㅓ 라인명/공정명/설비명/정지사유명을 전부 필요로 해서 .include
            // 각각 따로 조회하면 요청 하나에 쿼리가 5~6번 나가게 된다
            var log = await _db.WorkLogs
                .AsNoTracking()
                .Include(w => w.Worker)
                .Include(w => w.WorkOrder).ThenInclude(o => o.Line)
                .Include(w => w.WorkOrder).ThenInclude(o => o.Process)
                .Include(w => w.Pauses).ThenInclude(p => p.PauseReason)
                .FirstOrDefaultAsync(w =>
                    w.WorkerId == workerId &&
                    (w.Status == WorkLogStatus.InProgress || w.Status == WorkLogStatus.Paused));

            // 활성 건이 없는 건 정상 상태(작업 시작 전) — 예외가 아니라 null로 알린다
            if (log is null) return null;

            // 누적 실적 = 같은 작업지시에서 이미 완료된 이력들의 실적 합.
            // 화면이 "목표 500 / 누적 320 / 잔여 180"을 보여주는 데 쓴다
            var completedQty = await _db.WorkLogs
                .Where(w => w.WorkOrderId == log.WorkOrderId && w.Status == WorkLogStatus.Completed)
                .SumAsync(w => w.ActualQty);

            return new ActiveWorkLogResponse(
                log.Id,
                log.WorkOrderId,
                log.WorkerId,
                log.Worker.Name,
                log.WorkOrder.Line.Name,
                log.WorkOrder.Process.Name,
                log.Equipment?.Name,
                log.WorkOrder.TargetQty,
                completedQty,
                log.Status.ToString(),
                log.StartTime,
                log.Pauses
                    .OrderBy(p => p.PausedAt)
                    .Select(p => new WorkLogPauseResponse(
                        p.Id,
                        p.PauseReason.Name,
                        p.PauseReason.Category.ToString(),
                        p.PausedAt,
                        p.ResumedAt))
                    .ToList());
        }



        // WorkLog → WorkLogResponse 변환 — Start/Pause/Resume/Complete 네 곳이 전부 같은 모양을 반환하므로 공통화
        private static WorkLogResponse ToResponse(WorkLog log)
        {
            var isCompleted = log.Status == WorkLogStatus.Completed;

            return new WorkLogResponse(
                log.Id,
                log.WorkOrderId,
                log.WorkerId,
                log.Status.ToString(),
                log.StartTime,
                log.EndTime,
                isCompleted ? log.ActualQty : null,   // 완료 전엔 0이 아니라 null로 노출 (EndTime과 같은 규칙)
                log.ElapsedMinutes,
                log.OperatingMinutes,
                log.NetOperatingMinutes);
        }
    }
}