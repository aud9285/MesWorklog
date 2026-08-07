namespace MesWorklog.Models;


// 기능: 대기 → 진행중 ↔ 정지중 → 완료 순서로만 전이 가능. 역행/스킵은 WorkLog의 각 메서드가 차단
public enum WorkLogStatus
{
    InProgress, // 진행중상태
    Paused,     // 정지상태
    Completed   // 완료상태
}