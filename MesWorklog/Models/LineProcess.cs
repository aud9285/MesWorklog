namespace MesWorklog.Models
{
    public class LineProcess
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티

        // Lineid + ProcessId 복합키로 PK 설정, Id는 필요없음
        //public int Id { get; set; }


        // FK 프로퍼티와 네비게이션 프로퍼티
        // EF Core가 자동으로 FK 인식

        // 라인
        public int LineId { get; set; }
        
        // id값만 필요해서 주석처리
        //public Line Line { get; set; } = default!;

        // 공정
        public int ProcessId { get; set; }

        // id값만 필요해서 주석처리
        //public Process Process { get; set; } = default!;

    }
}
