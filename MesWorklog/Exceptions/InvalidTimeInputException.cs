namespace MesWorklog.Exceptions
{
    // 시간 입력 규칙 위반시 익셉션(종료 시간이 시작 시간보다 빠른경우등)
    public class InvalidTimeInputException : Exception
    {
        public InvalidTimeInputException(string message) : base(message) { }
    }
}
