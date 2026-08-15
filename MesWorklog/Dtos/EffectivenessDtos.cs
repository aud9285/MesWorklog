namespace MesWorklog.Dtos
{

    // record는 값을 담기만 하는 타입을 짧게 쓰는 문법
    // record는 GroupKey/GroupName/OperatingMinutes/NetOperatingMinutes 프로퍼티 + 생성자 + Equals + ToString 을 전부 만들어준다.
    public record EffectivenessResponse(int? GroupKey, string GroupName, long OperatingMinutes, long NetOperatingMinutes)
    {
        // 계산 프로퍼티 생성자로 값을 안받고 4개필드로 매번 계산
        // 가동률 = Σ가동시간 / Σ부하시간 * 100
        // 삼항연산자
        // 조건이 (RatePercent => OperatingMinutes == 0)
        // true면(?) = 0
        // false면(:) =  Math.Round((double)NetOperatingMinutes / OperatingMinutes * 100, 1)
        public double RatePercent => OperatingMinutes == 0
            ? 0
            // double 캐스팅 안할시 정수형이라 소수점이 날라감, 소수첫째자리까지
            : Math.Round((double)NetOperatingMinutes / OperatingMinutes * 100, 1);
    }

}
