namespace MesWorklog.Models
{
    public class Process
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id{ get; set; }

        // 이 공정의 상위 라인목록, 하나일 수도, 복수일 수도 있음
        public ICollection<LineProcess> LineProcesses { get; set; } = new List<LineProcess>();

        // 이 공정의 배정된 작업자 목록
        public ICollection<WorkerProcess> WorkerProcesses { get; set; } = new List<WorkerProcess>();

        // 이공정에서 발생한 작업지시 목록
        public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    }
}
