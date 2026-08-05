namespace MesWorklog.Models
{
    public class Worker
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id { get; set; }

        // 컴파일시 경고방지를 위해 default! 사용
        public string Name { get; set; } = default!;

        // FK 프로퍼티와 네비게이션 프로퍼티
        // EF Core가 자동으로 FK 인식
        public int ProcessId { get; set; }
        public Process Process { get; set; } = default!;
    
    }
}
