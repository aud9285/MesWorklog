namespace MesWorklog.Models
{
    public class Worker
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id { get; set; }

        // 컴파일시 경고방지를 위해 default! 사용
        public string Name { get; set; } = default!;

        // 삭제시 논리삭제를 위해 IsActive 속성 추가, true이면 활성, false이면 비활성
        public bool IsActive { get; set; } = true;

        // 작업자가 배정된 공정 목록 N:M
        public ICollection<WorkerProcess> WorkerProcesses { get; set; } = new List<WorkerProcess>();

        // 대시보드는 dapper로 구현해서 사용하지 않아 주석처리
        //public ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();

    }
}
