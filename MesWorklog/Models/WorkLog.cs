using MesWorklog.Exceptions;

namespace MesWorklog.Models
{
    public class WorkLog
    {

        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id { get; set; }

        // 어느 작업지시의 작업내역인지
        public int WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; } = default!;

        // 어느 작업자인지
        public int WorkerId { get; set; }
        public Worker Worker { get; set; } = default!;

        // 필드 추가 (다른 필드들처럼 private set — Start()를 통해서만 채워짐)
        public int? EquipmentId { get; private set; }
        public Equipment? Equipment { get; private set; }

        // private로 설정하여 외부에서 값을 변경하지 못하도록 함
        // waiting 상태(최초의 상태(시작하기 전상태))
        public WorkLogStatus Status { get; private set; }

        // 작업 시작시간
        public DateTime StartTime { get; private set; }
        // 작업 종료시간
        // 완료전은 NULL, 완료후는 종료시간
        public DateTime? EndTime { get; private set; }

        // 실적 수량 작업자 작업완료 시 입력 받음.
        public int ActualQty { get; private set; }

        // 조업시간 = 종료시간 - 시작시간 작업완료시 계산
        public int? ElapsedMinutes { get; private set; }

        // 부하시간 = 조업시간 - 계획정지시간
        public int? OperatingMinutes { get; private set; }

        // 가동시간 = 부하시간 - 비가동시간
        public int? NetOperatingMinutes { get; private set; }

        // 작업 정지이력 리스트 
        public ICollection<WorkLogPause> Pauses { get; private set; } = new List<WorkLogPause>();


        // EF Core에서 DB읽어올 때만 쓰는 생성자
        private WorkLog() { }


        // 작업 시작
        // 미래시간 입력 방지
        public static WorkLog Start(int workOrderId, int workerId, int? equipmentId, DateTime startTime, DateTime now)
        {
            ValidateNotFuture(startTime, now, "시작 시각");

            return new WorkLog
            {
                WorkOrderId = workOrderId,
                WorkerId = workerId,
                EquipmentId = equipmentId,
                StartTime = startTime,
                Status = WorkLogStatus.InProgress
            };
        }


        // 작업 중지(IN_PROGRESS 상태에만 작업중지 가능)
        // 연속정지 방지
        public void Pause(DateTime pausedAt, PauseReason pauseReason, DateTime now)
        {
            if (Status != WorkLogStatus.InProgress)
                throw new InvalidWorkLogStateException("진행중 상태에서만 정지할 수 있습니다.");

            ValidateNotFuture(pausedAt, now, "정지 시각");


            // 직전 시작/재개 시간을 구해서, 이번 정지시간이 더 과거입력 방지
            // HasValue로 null 체크
            var lastResumedAt = Pauses
                .Where(p => p.ResumedAt.HasValue)
                .Select(p => p.ResumedAt!.Value)
                .DefaultIfEmpty(StartTime)
                .Max();

            if (pausedAt < lastResumedAt)
                throw new InvalidTimeInputException("정지 시각은 직전 시작/재개 시각보다 이후여야 합니다.");


            // 정지 이력 1건 추가. WorkLog = this로 네비게이션만 세팅(Id는 저장 전이라 0일 수 있어 FK로 직접 안 씀)
            // PauseReason 네비게이션까지 바로 채워서 저장 — Complete()가 이후 이 WorkLog를 어떤 쿼리
            // 모양으로 불러오든 p.PauseReason.Category를 안전하게 읽을 수 있음 (더 이상 .ThenInclude에 의존 안 함)
            Pauses.Add(new WorkLogPause { PauseReason = pauseReason, PausedAt = pausedAt, WorkLog = this });
            Status = WorkLogStatus.Paused;
        }

        // 작업 재개(PAUSE 상태에만 가능) 재개시 재개시간 저장후 상태 진행중으로 변경
        public void Resume(DateTime resumedAt, DateTime now)
        {
            if (Status != WorkLogStatus.Paused)
                throw new InvalidWorkLogStateException("정지중 상태에서만 재개할 수 있습니다.");

            ValidateNotFuture(resumedAt, now, "재개 시각");

            // 기능: PAUSED 상태인데 열린 정지가 없으면 데이터 모순이므로 방어적으로 예외
            var openPause = Pauses.FirstOrDefault(p => p.ResumedAt == null)
                ?? throw new InvalidWorkLogStateException("정지 중인데 현재 정지 이력을 찾을 수 없습니다.");

            if (resumedAt <= openPause.PausedAt)
                throw new InvalidTimeInputException("재개 시각은 정지 시각보다 이후여야 합니다.");

            openPause.ResumedAt = resumedAt;
            Status = WorkLogStatus.InProgress;
        }

        // 완료(IN_PROGRESS 상태에만 완료 가능)
        public void Complete(DateTime endTime, int actualQty, DateTime now)
        {
            if (Status != WorkLogStatus.InProgress)
                throw new InvalidWorkLogStateException("진행중 상태에서만 완료할 수 있습니다.");

            ValidateNotFuture(endTime, now, "종료 시각");

            // IN_PROGRESS에서는 모든 정지가 재개상태, 마지막 재개시각(정지가 없으면 시작시각)이 마지막 기록 시각
            var lastRecordedAt = Pauses
                .Where(p => p.ResumedAt.HasValue)
                .Select(p => p.ResumedAt!.Value)
                .DefaultIfEmpty(StartTime)
                .Max();

            if (endTime < lastRecordedAt)
                throw new InvalidTimeInputException("종료 시각은 마지막 시작/재개 시각보다 이후여야 합니다.");

            EndTime = endTime;
            ActualQty = actualQty;
            Status = WorkLogStatus.Completed;


            // 조업 시간 계산 
            // TotalMinutes = 시간 계산을 분으로 가져옴
            ElapsedMinutes = (int)(endTime - StartTime).TotalMinutes;

            // 계획정지 구간 합(분) — 부하시간 계산에서 차감분
            var plannedPauseMinutes = Pauses
                .Where(p => p.PauseReason.Category == PauseCategory.Planned && p.ResumedAt.HasValue)
                .Sum(p => (int)(p.ResumedAt!.Value - p.PausedAt).TotalMinutes);

            // 비가동 구간 합(분) — 가동시간 계산에서 차감분
            var unplannedPauseMinutes = Pauses
                .Where(p => p.PauseReason.Category == PauseCategory.Unplanned && p.ResumedAt.HasValue)
                .Sum(p => (int)(p.ResumedAt!.Value - p.PausedAt).TotalMinutes);

            OperatingMinutes = ElapsedMinutes - plannedPauseMinutes;
            NetOperatingMinutes = OperatingMinutes - unplannedPauseMinutes;
        }

        //  모든 시각 입력 공통 규칙 — 미래 시각 금.
        //      now를 파라미터로 받는 건 KoreaTime.Now를 호출부에서 주입해 테스트에서 시계를 고정하기 위함
        private static void ValidateNotFuture(DateTime time, DateTime now, string fieldLabel)
        {
            if (time > now)
                throw new InvalidTimeInputException($"{fieldLabel}은(는) 미래 시각일 수 없습니다.");
        }

    }
}
