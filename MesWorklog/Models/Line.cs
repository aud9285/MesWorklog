namespace MesWorklog.Models
{
    public class Line
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티
        public int Id { get; set; }

        // 컴파일시 경고방지를 위해 default! 사용
        public string Name { get; set; } = default!;

        // 삭제시 논리삭제를 위해 IsActive 속성 추가, true이면 활성, false이면 비활성
        public bool IsActive { get; set; } = true;

        // 라인에 속한 공정목록 
        public ICollection<LineProcess> LineProcesses { get; set; } = new List<LineProcess>();
    }
}
