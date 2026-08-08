namespace MesWorklog.Models
{
    public class WorkerProcess
    {
        // 자동 pk auto-incremaent
        // EF core C# 자동 프로퍼티

        // Workerid + ProcessId 복합키로 설정해서 Id는 필요없음
        //public int Id { get; set; }

        // 어느 작업자인지
        public int WorkerId { get; set; }

        // id값만 필요해서 주석처리
        //public Worker Worker { get; set; } = default!;


        // 어느 공정에 속해있는지
        public int ProcessId { get; set; }

        // id값만 필요해서 주석처리
        //public Process Process { get; set; } = default!;
    }
}
