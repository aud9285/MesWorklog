namespace MesWorklog.Models
{
    public class Equipment
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id { get; set; }

        // 컴파일시 경고방지를 위해 default! 사용
        public string Name { get; set; } = default!;

        // 삭제시 논리삭제를 위해 IsActive 속성 추가, true이면 활성, false이면 비활성
        public bool IsActive { get; set; } = true;

        // 이 설비를 사용한 작업지시 목록
        // 실사용하지 않아 주석처리
        //public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    }
}
