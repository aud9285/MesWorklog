namespace MesWorklog.Exceptions
{
    // 라인-공정이 LineProcess테이블에 없을 때
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string message) : base(message) { }
    }
}
