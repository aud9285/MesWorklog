using MesWorklog.Models;
using Microsoft.EntityFrameworkCore;

namespace MesWorklog.Data
{

    // DbContext를 상속받아 EF Core에서 사용할 데이터베이스 컨텍스트를 정의
    // Dbcontext는 연결 관리, 변경 추적, sql 생성 기능
    public class AppDbContext : DbContext
    {

        // DI 컨테이너가 options를 통해 DbContextOptions<AppDbContext>를 주입하도록 구성
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }


        // 이 컨텍스트에서 관리하는 테이블
        // 없는 엔티티는 EF Core가 인식하지않음
        public DbSet<Line> Lines => Set<Line>();
        public DbSet<Process> Processes => Set<Process>();
        public DbSet<Worker> Workers => Set<Worker>();
        public DbSet<Equipment> Equipments => Set<Equipment>();
        public DbSet<PauseReason> PauseReasons => Set<PauseReason>();
        public DbSet<LineProcess> LineProcesses => Set<LineProcess>();
        public DbSet<WorkerProcess> WorkerProcesses => Set<WorkerProcess>();
        public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
        public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
        public DbSet<WorkLogPause> WorkLogPauses => Set<WorkLogPause>();


        // 매핑 규칙 선언
        // 이 dbcontext에를 통해 데이터베이스에 처음 접근할때 딱 한번만 호출 그리고 메모리를 캐시해두고 재사용
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            // modelBuilder란 매핑 규칙을 등록하는 도구
            // modelBuilder.Entity<T>() : T 엔티티에 대한 매핑 규칙을 등록하는 메서드

            // 복합키 설정
            // LineProcess, WorkerProcess 엔티티의 복합키 설정(LineProcess, WorkerProcess는 각각 LineId,ProcessId와 WorkerId, ProcessId를 복합키로 사용)
            modelBuilder.Entity<LineProcess>().HasKey(lp => new { lp.LineId, lp.ProcessId });
            modelBuilder.Entity<WorkerProcess>().HasKey(wp => new { wp.WorkerId, wp.ProcessId });


            // 조인 테이블 관계 Cascade


            // 마스터 데이터 삭제시
            modelBuilder.Entity<LineProcess>()
                .HasOne<Line>().WithMany(l => l.LineProcesses)          // HasOne() : LineProcess는 Line을 하나만 참조, WithMany() : Line은 여러개의 LineProcess를 참조(1:N 관계)
                .HasForeignKey(lp => lp.LineId)                         // hasforeignKey() : LineProcess의 외래키는 LineId
                .OnDelete(DeleteBehavior.Cascade);                      // Line이 지워지면 이 매핑 행도 함께 삭제

            modelBuilder.Entity<LineProcess>()
                .HasOne<Process>().WithMany(p => p.LineProcesses)
                .HasForeignKey(lp => lp.ProcessId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WorkerProcess>()
                .HasOne<Worker>().WithMany(w => w.WorkerProcesses)
                .HasForeignKey(wp => wp.WorkerId)
                .OnDelete(DeleteBehavior.Cascade);

            // WithMany()에 인자가 없는 이유: Process.WorkerProcesses 컬렉션을 안 만들었기 때문.
            // EF Core는 한쪽에만 네비게이션이 있어도 관계를 잡아줌 (단방향 관계)
            modelBuilder.Entity<WorkerProcess>()
                .HasOne<Process>().WithMany()
                .HasForeignKey(wp => wp.ProcessId)
                .OnDelete(DeleteBehavior.Cascade);


            // 작업지시 삭제시 그이력도 삭제, 현재 작업지시 삭제는 작업이력을 삭제 시 해당 작업지시의 남아있는 작업이력이 없으면 삭제
            modelBuilder.Entity<WorkLog>()
                .HasOne(w => w.WorkOrder).WithMany(o => o.WorkLogs)     // 작업이력 하나는 작업지시 하나의 속하고, 그 작업지시는 작업이력을 여러개를 가진다
                .HasForeignKey(w => w.WorkOrderId)                      
                .OnDelete(DeleteBehavior.Cascade);

            // 작업이력 삭제시 정지이력 삭제
            modelBuilder.Entity<WorkLogPause>()
                .HasOne(p => p.WorkLog).WithMany(w => w.Pauses)
                .HasForeignKey(p => p.WorkLogId)
                .OnDelete(DeleteBehavior.Cascade);



            // 이력 참조 Restrict
            // EF Core는 기본적으로 부모 엔티티가 삭제되면 자식 엔티티도 삭제(Cascade)하도록 설정되어 있음
            // 하지만 WorkOrder, WorkLog, WorkLogPause는 과거 이력 데이터이므로 부모가 삭제되더라도 자식은 삭제되지 않도록 Restrict로 설정

            // 공정삭제 시, 이 공정으로 진행한 작업지시가 있을경우 제한
            modelBuilder.Entity<WorkOrder>()
                .HasOne(o => o.Process).WithMany()
                .HasForeignKey(o => o.ProcessId)
                .OnDelete(DeleteBehavior.Restrict);     // 자식이 있으면 삭제 제한

            // 라인삭제 시, 이 라인에서 진행한 작업지시가 있을경우 제한
            modelBuilder.Entity<WorkOrder>()
                .HasOne(o => o.Line).WithMany()
                .HasForeignKey(o => o.LineId)
                .OnDelete(DeleteBehavior.Restrict);

            // EquipmentId는 nullable(int?)이라(수작업 케이스 때문에 null 허용) EF Core 기본값은 "부모 삭제 시 null로 만들기" 인데,
            // 그러면 "이 작업을 어느 설비로 했는가"라는 과거 사실이 조용히 지워짐 → Restrict로 명시
            modelBuilder.Entity<WorkOrder>()
                .HasOne(o => o.Equipment).WithMany()
                .HasForeignKey(o => o.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 작업자삭제 시, 이 작업자가 진행한 작업이력가 있을경우 제한
            modelBuilder.Entity<WorkLog>()
                .HasOne(w => w.Worker).WithMany()
                .HasForeignKey(w => w.WorkerId)
                .OnDelete(DeleteBehavior.Restrict);

            // 정지이유 삭제시 작업정지이력 삭제 제한
            // 현재 웹 flow상에서 정지이유 삭제 flow는 없음, 직접 DB에서 insert, delete 하는식으로 설계되어있음(정지이유는 거의 정해져있고, 추가하거나 삭제하는일은 거의 없음
            modelBuilder.Entity<WorkLogPause>()
                .HasOne(p => p.PauseReason).WithMany()
                .HasForeignKey(p => p.PauseReasonId)
                .OnDelete(DeleteBehavior.Restrict);



            // enum 저장 방식

            // enum은 DB에 정수 0, 1, 2로 저장됨,
            // Dapper로 쓸 쿼리가 문자열로 비교되는데 정수로 저장되어 있으면 조건이 안맞음
            // 문자열로 저장하도록 변환기 지정
            modelBuilder.Entity<WorkLog>()
                // HasConversion<string>() : string으로 변환해서 저장
                // HasMaxLength(20) : varcher(20)
                .Property(w => w.Status).HasConversion<string>().HasMaxLength(20);
            modelBuilder.Entity<PauseReason>()
                .Property(r => r.Category).HasConversion<string>().HasMaxLength(20);



            // 인덱싱
            // 현재 차트가 StartTime 범위로 조회(일/주/월/년 날짜별 집계)
            modelBuilder.Entity<WorkLog>().HasIndex(w => w.StartTime);


            // 문자열 길이
            // C#은 string 길이 개념이 없어서, 문자열 길이 설정 안할 시 무제한 으로 만들어짐
            modelBuilder.Entity<Line>().Property(l => l.Name).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<Process>().Property(p => p.Name).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<Worker>().Property(w => w.Name).HasMaxLength(50).IsRequired();
            modelBuilder.Entity<Equipment>().Property(e => e.Name).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<PauseReason>().Property(r => r.Name).HasMaxLength(100).IsRequired();



            // 논리명

            // 라인
            modelBuilder.Entity<Line>().ToTable(t => t.HasComment("라인"));
            modelBuilder.Entity<Line>().Property(l => l.Id).HasComment("라인 ID");
            modelBuilder.Entity<Line>().Property(l => l.Name).HasComment("라인명");
            modelBuilder.Entity<Line>().Property(l => l.IsActive).HasComment("사용여부");

            // 공정
            modelBuilder.Entity<Process>().ToTable(t => t.HasComment("공정"));
            modelBuilder.Entity<Process>().Property(p => p.Id).HasComment("공정 ID");
            modelBuilder.Entity<Process>().Property(p => p.Name).HasComment("공정명");
            modelBuilder.Entity<Process>().Property(p => p.IsActive).HasComment("사용여부");

            // 작업자
            modelBuilder.Entity<Worker>().ToTable(t => t.HasComment("작업자"));
            modelBuilder.Entity<Worker>().Property(w => w.Id).HasComment("작업자 ID");
            modelBuilder.Entity<Worker>().Property(w => w.Name).HasComment("작업자명");
            modelBuilder.Entity<Worker>().Property(w => w.IsActive).HasComment("사용여부");

            // 설비
            modelBuilder.Entity<Equipment>().ToTable(t => t.HasComment("설비"));
            modelBuilder.Entity<Equipment>().Property(e => e.Id).HasComment("설비 ID");
            modelBuilder.Entity<Equipment>().Property(e => e.Name).HasComment("설비명");
            modelBuilder.Entity<Equipment>().Property(e => e.IsActive).HasComment("사용여부");

            // 정지사유
            modelBuilder.Entity<PauseReason>().ToTable(t => t.HasComment("정지사유"));
            modelBuilder.Entity<PauseReason>().Property(r => r.Id).HasComment("정지사유 ID");
            modelBuilder.Entity<PauseReason>().Property(r => r.Name).HasComment("정지사유명");
            modelBuilder.Entity<PauseReason>().Property(r => r.Category).HasComment("분류(Planned=계획정지, Unplanned=비가동)");

            // 라인-공정 매핑
            modelBuilder.Entity<LineProcess>().ToTable(t => t.HasComment("라인-공정"));
            modelBuilder.Entity<LineProcess>().Property(lp => lp.LineId).HasComment("라인 ID");
            modelBuilder.Entity<LineProcess>().Property(lp => lp.ProcessId).HasComment("공정 ID");

            // 작업자-공정 매핑
            modelBuilder.Entity<WorkerProcess>().ToTable(t => t.HasComment("작업자-공정"));
            modelBuilder.Entity<WorkerProcess>().Property(wp => wp.WorkerId).HasComment("작업자 ID");
            modelBuilder.Entity<WorkerProcess>().Property(wp => wp.ProcessId).HasComment("공정 ID");

            // 작업지시
            modelBuilder.Entity<WorkOrder>().ToTable(t => t.HasComment("작업지시"));
            modelBuilder.Entity<WorkOrder>().Property(o => o.Id).HasComment("작업지시 ID");
            modelBuilder.Entity<WorkOrder>().Property(o => o.ProcessId).HasComment("공정 ID");
            modelBuilder.Entity<WorkOrder>().Property(o => o.LineId).HasComment("실행 라인 ID");
            modelBuilder.Entity<WorkOrder>().Property(o => o.EquipmentId).HasComment("설비 ID(수작업이면 NULL)");
            modelBuilder.Entity<WorkOrder>().Property(o => o.TargetQty).HasComment("목표 수량");
            modelBuilder.Entity<WorkOrder>().Property(o => o.CompletedAt).HasComment("완료 시각");

            // 작업이력
            modelBuilder.Entity<WorkLog>().ToTable(t => t.HasComment("작업이력(로그)"));
            modelBuilder.Entity<WorkLog>().Property(w => w.Id).HasComment("작업이력 ID");
            modelBuilder.Entity<WorkLog>().Property(w => w.WorkOrderId).HasComment("작업지시 ID");
            modelBuilder.Entity<WorkLog>().Property(w => w.WorkerId).HasComment("작업자 ID");
            modelBuilder.Entity<WorkLog>().Property(w => w.Status).HasComment("상태(InProgress=진행중, Paused=정지중, Completed=완료)");
            modelBuilder.Entity<WorkLog>().Property(w => w.StartTime).HasComment("작업 시작시각");
            modelBuilder.Entity<WorkLog>().Property(w => w.EndTime).HasComment("작업 종료시각");
            modelBuilder.Entity<WorkLog>().Property(w => w.ActualQty).HasComment("실적 수량");
            modelBuilder.Entity<WorkLog>().Property(w => w.ElapsedMinutes).HasComment("조업시간(분)");
            modelBuilder.Entity<WorkLog>().Property(w => w.OperatingMinutes).HasComment("가동시간(분)");
            modelBuilder.Entity<WorkLog>().Property(w => w.NetOperatingMinutes).HasComment("실가동시간(분)");

            // 정지이력
            modelBuilder.Entity<WorkLogPause>().ToTable(t => t.HasComment("작업 정지이력"));
            modelBuilder.Entity<WorkLogPause>().Property(p => p.Id).HasComment("정지이력 ID");
            modelBuilder.Entity<WorkLogPause>().Property(p => p.WorkLogId).HasComment("작업이력 ID");
            modelBuilder.Entity<WorkLogPause>().Property(p => p.PauseReasonId).HasComment("정지사유 ID");
            modelBuilder.Entity<WorkLogPause>().Property(p => p.PausedAt).HasComment("정지 시각");
            modelBuilder.Entity<WorkLogPause>().Property(p => p.ResumedAt).HasComment("재개 시각");

            base.OnModelCreating(modelBuilder);
        }




    }
}
