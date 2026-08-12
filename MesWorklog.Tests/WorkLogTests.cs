using MesWorklog.Exceptions;
using MesWorklog.Models;

namespace MesWorklog.Tests;

public class WorkLogTests
{
    // 모든 테스트가 공유하는 고정 시계.
    private static readonly DateTime Now = new(2026, 8, 9, 23, 0, 0);

    // 자주 쓰는 시각을 짧게 쓰기 위해서
    private static DateTime At(int hour, int minute = 0) => new(2026, 8, 9, hour, minute, 0);

    // 테스트용 정지사유 — Category가 중요하지 않은 테스트는 기본값(Planned)을 씀
    private static PauseReason Reason(PauseCategory category = PauseCategory.Planned)
        => new PauseReason { Name = "테스트사유", Category = category };

    // ── Start ────────────────────────────────────────────────

    // [Fact] = 파라미터 없는 단일 테스트
    [Fact]
    public void 시작하면_진행중_상태가_된다()
    {
        //시작
        var log = WorkLog.Start(workOrderId: 1, workerId: 1, startTime: At(9), now: Now);

        Assert.Equal(WorkLogStatus.InProgress, log.Status);
        Assert.Equal(At(9), log.StartTime);
        Assert.Null(log.EndTime);
        Assert.Empty(log.Pauses);
    }

    [Fact]
    public void 미래_시각으로는_시작할_수_없다()
    {
        // AWS(UTC) 배포 시 오전 입력이 전부 미래로 판정되던 문제 때문에 KST로 단일화했고
        // 그 기준시각을 파라미터로 받으므로 여기서 now보다 뒤인 값을 넣어 검증한다
        Assert.Throws<InvalidTimeInputException>(
            () => WorkLog.Start(1, 1, startTime: Now.AddMinutes(10), now: Now));
    }

    // ── Pause ────────────────────────────────────────────────

    [Fact]
    public void 정지하면_열린_정지_이력이_한_건_생긴다()
    {
        var log = WorkLog.Start(1, 1, At(9), Now);

        log.Pause(pausedAt: At(10), pauseReason: Reason(), now: Now);

        Assert.Equal(WorkLogStatus.Paused, log.Status);
        var pause = Assert.Single(log.Pauses);
        Assert.Equal(At(10), pause.PausedAt);
        Assert.Null(pause.ResumedAt);   // 아직 재개 전이라 재개시간이 null
    }

    [Fact]
    public void 연속으로_정지할_수_없다()
    {
        // 정지를 연달아 호출하면 열린 정지가 2건 이상 생겨 시간 계산이 무한정 커졌다
        var log = WorkLog.Start(1, 1, At(9), Now);
        log.Pause(At(10), Reason(), Now);

        Assert.Throws<InvalidWorkLogStateException>(() => log.Pause(At(11), Reason(), Now));
    }

    [Fact]
    public void 정지_시각은_직전_재개_시각보다_이후여야_한다()
    {
        // 정지 구간이 서로 겹치면 합산이 중복돼 가동률이 왜곡된다
        var log = WorkLog.Start(1, 1, At(9), Now);
        log.Pause(At(10), Reason(), Now);
        log.Resume(At(10, 30), Now);

        // 10:30에 재개했는데 10:20에 정지 → 겹침
        Assert.Throws<InvalidTimeInputException>(() => log.Pause(At(10, 20), Reason(), Now));
    }

    // ── Resume ───────────────────────────────────────────────

    [Fact]
    public void 재개하면_열린_정지가_닫히고_진행중이_된다()
    {
        var log = WorkLog.Start(1, 1, At(9), Now);
        log.Pause(At(10), Reason(), Now);

        log.Resume(At(10, 30), Now);

        // 진행중에는 정지중이 존재하지 않음
        Assert.Equal(WorkLogStatus.InProgress, log.Status);
        Assert.Equal(At(10, 30), log.Pauses.Single().ResumedAt);
    }

    [Fact]
    public void 진행중일_때는_재개할_수_없다()
    {
        var log = WorkLog.Start(1, 1, At(9), Now);

        Assert.Throws<InvalidWorkLogStateException>(() => log.Resume(At(10), Now));
    }

    // ── Complete ─────────────────────────────────────────────

    [Fact]
    public void 정지중에는_완료할_수_없다()
    {
        // PAUSED에서 완료하면 열린 정지가 남는데, 그 구간은 정지 합산에서는 제외되면서
        // ElapsedMinutes에는 포함돼 가동률이 100%로 부풀려졌다.
        // 가드를 IN_PROGRESS로 좁혔다
        var log = WorkLog.Start(1, 1, At(9), Now);
        log.Pause(At(10), Reason(), Now);

        Assert.Throws<InvalidWorkLogStateException>(() => log.Complete(At(12), 10, Now));
    }

    [Fact]
    public void 종료_시각은_마지막_기록_시각보다_이후여야_한다()
    {
        var log = WorkLog.Start(1, 1, At(9), Now);
        log.Pause(At(10), Reason(), Now);
        log.Resume(At(11), Now);

        // 11:00에 재개했는데 10:30에 종료 → 시간 역전
        Assert.Throws<InvalidTimeInputException>(() => log.Complete(At(10, 30), 10, Now));
    }

    [Fact]
    public void 정지가_없으면_세_시간이_모두_같다()
    {
        var log = WorkLog.Start(1, 1, At(9), Now);

        log.Complete(endTime: At(12), actualQty: 10, now: Now);

        Assert.Equal(WorkLogStatus.Completed, log.Status);
        Assert.Equal(10, log.ActualQty);
        // 조업 = 가동 = 실가동 = 180분 → 가동률 100%
        Assert.Equal(180, log.ElapsedMinutes);
        Assert.Equal(180, log.OperatingMinutes);
        Assert.Equal(180, log.NetOperatingMinutes);
    }

    [Fact]
    public void 계획정지와_비가동이_각각_차감된다()
    {
        // 09:00 시작 → 10:00~10:30 식사(계획정지 30분) → 11:00~11:20 고장(비가동 20분) → 12:00 완료
        var log = WorkLog.Start(1, 1, At(9), Now);

        // Pause() 호출 시점에 바로 카테고리를 넘김 — 예전 SetCategory 후처리가 더 이상 필요 없음
        log.Pause(At(10), Reason(PauseCategory.Planned), Now);     // 식사
        log.Resume(At(10, 30), Now);
        log.Pause(At(11), Reason(PauseCategory.Unplanned), Now);   // 설비 고장
        log.Resume(At(11, 20), Now);

        log.Complete(At(12), 10, Now);

        Assert.Equal(180, log.ElapsedMinutes);        // 조업   = 12:00 - 09:00
        Assert.Equal(150, log.OperatingMinutes);      // 가동   = 180 - 30(계획정지)
        Assert.Equal(130, log.NetOperatingMinutes);   // 실가동 = 150 - 20(비가동)
        // → 가동률 130/180 = 72.2%. 정지를 기록할수록 수치가 내려가는 게 정상 동작이다
    }
}
