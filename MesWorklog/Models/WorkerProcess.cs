namespace MesWorklog.Models
{
    public class WorkerProcess
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id { get; set; }

        // 어느 작업자인지
        public string WorkerId { get; set; } = default!;
        public Worker Worker { get; set; } = default!;


        // 어느 공정에 속해있는지
        public int ProcessId { get; set; }

        public Process Process { get; set; } = default!;
    }
}
