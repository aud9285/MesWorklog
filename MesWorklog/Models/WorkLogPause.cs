namespace MesWorklog.Models
{
    public class WorkLogPause
    {

        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id { get; set; }


        // 어느 작업이력의 정지인지
        public int WorkLogId { get; set; }
        public WorkLog WorkLog { get; set; } = default!;

        // 정지사유
        public int PauseReasonId { get; set; }
        public PauseReason PauseReason { get; set; } = default!;

        // 정지 시작 시간
        public DateTime PausedAt { get; set; }

        // 정지 종료 시간(재개시간)
        // 재개 전이라면 NULL, 재개 후라면 종료시간
        public DateTime? ResumedAt { get; set; }

    }
}
