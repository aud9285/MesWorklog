namespace MesWorklog.Models
{
    public class Process
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id{ get; set; }

        // 컴파일시 경고방지를 위해 default! 사용
        public string Name { get; set; } = default!;        // 공정명
        public string LineName { get; set; } = default!;    // 라인명
    }
}
