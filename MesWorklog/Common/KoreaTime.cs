namespace MesWorklog.Common
{
    // 시간 기준 서울 시간 고정
    public class KoreaTime
    {

        // asia/seoul 시간대를 정보를 가져옴
        private static readonly TimeZoneInfo KstZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KstZone);
    }
}
