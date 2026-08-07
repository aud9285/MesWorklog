namespace MesWorklog.Exceptions
{
    // 작업자 상태 변경시 규칙 위반시 익셉션(진행중 -> 정지중, 정지중 -> 진행중, 진행중/정지중 -> 완료 순서로만 전이 가능)
    public class InvalidWorkLogStateException : Exception
    {
        public InvalidWorkLogStateException(string message) : base(message) { }
    }
}
