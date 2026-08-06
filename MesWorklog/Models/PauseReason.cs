namespace MesWorklog.Models
{
    public class PauseReason
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id { get; set; }

        // 컴파일시 경고방지를 위해 default! 사용
        public string Name { get; set; } = default!;
        
        // 계획정지, 비가동 분류
        public PauseCategory Category { get; set; }

        public ICollection<WorkLogPause> WorkLogPauses { get; set; } = new List<WorkLogPause>();

    }
}
