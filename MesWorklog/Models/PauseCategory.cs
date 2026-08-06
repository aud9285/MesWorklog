namespace MesWorklog.Models
{
    // 계획정지시간은 조업시간에서만 차감, 비가동시간은 계획정지시간을 차감한 가동시간에서 차감
    public enum PauseCategory
    {
        Planned,    // 계획정지 (식사, 정기점검 등)
        Unplanned   // 비가동 (고장, 자재대기 등)
    }
}
