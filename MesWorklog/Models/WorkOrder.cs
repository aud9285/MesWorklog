namespace MesWorklog.Models
{
    public class WorkOrder
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id { get; set; }

        // 어느 공정의 작업지시인지
        public int ProcessId { get; set; }
        public Process Process { get; set; } = default!;

      
        // 이 작업지시가 실제로 실행된 라인
        public int LineId { get; set; }

       
        public Line Line { get; set; } = default!;

       
        // 설비 없이 수작업하는 경우도 있어 null 허용
        public int? EquipmentId { get; set; }

        // EquipmentId가 null이면 이것도 null
        public Equipment? Equipment { get; set; }


        // 목표 수량
        public int TargetQty { get; set; }

        // 작업자 완료수량 누적이 TargetQty에 도달한 시점. null이면 미완료 —
       
        public DateTime? CompletedAt { get; set; }

        // 이 작업지시에 딸린 실적 목록. 여러 작업자가 병렬로/이어서 작업할 수 있어 1:N
        public ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();
    }
}
