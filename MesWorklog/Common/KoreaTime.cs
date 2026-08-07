namespace MesWorklog.Common
{
    // 시간 기준 서울 시간 고정
    public class KoreaTime
    {
        private static readonly TimeZoneInfo KstZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KstZone);
    }
}
