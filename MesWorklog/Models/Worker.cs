namespace MesWorklog.Models
{
    public class Worker
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id { get; set; }

        // 컴파일시 경고방지를 위해 default! 사용
        public string Name { get; set; } = default!;

        // 작업자가 배정된 공정 목록 N:M
        public ICollection<WorkerProcess> WorkerProcesses { get; set; } = new List<WorkerProcess>();

        public ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();

    }
}
