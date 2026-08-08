using MesWorklog.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MesWorklog.Middleware
{
    // Controller에서 Exception이 발생하면 한곳에서 잡아 HTTP 응답으로 변환
    // .NET 8버전부터IExceptionHandler 방식으로 이전 try-catch에서 코드로 switch, if로 분기한던걸 값만 분기함
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        
        // 여기서는 DI로 주입받음. ILogger<T>의 T가 로그에 찍히는 카테고리 이름이 됨
        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        // 반환값 true  = 내가 처리했으니 파이프라인 여기서 종료
        // 반환값 false = 다음 핸들러로 넘김(아무도 처리 안 하면 기본 500 응답)
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            // switch 식(expression) + 타입 패턴 매칭
            // exception 이 어느 타입이냐로 분기해서 값을 고른다
            // status, title 처럼 값 두 개를 한 번에 받는 건 튜플(tuple)
            // 예외 종류에 따라 HTTP 상태 코드와 제목을 결정
            var (status, title) = exception switch
            {
                // 보낸 시간값이 잘못된 경우 — 미래 시각, 시간 순서 역전
                // => 람다x InvalidTimeInputException 패턴이면 (StatusCodes.Status400BadRequest, "시간 입력 오류") 이값 호출 이란 뜻
                InvalidTimeInputException => (StatusCodes.Status400BadRequest, "시간 입력 오류"),

                // 값이  현재 상태와 충돌 — 정지중인데 완료 시도 등
                InvalidWorkLogStateException => (StatusCodes.Status409Conflict, "작업 상태 오류"),

                // 업무 규칙 위반 — 라인-공정 조합 불일치, 마스터데이터 삭제 불가 등
                BusinessRuleException => (StatusCodes.Status409Conflict, "처리할 수 없는 요청"),

                // 서비스 계층에서 대상을 찾지 못했을 때
                KeyNotFoundException => (StatusCodes.Status404NotFound, "대상을 찾을 수 없음"),

                // _ 는 나머지 전부(default)
                // 그 외 나머지 오류 = 예상하지 못한 버그

                _ => (StatusCodes.Status500InternalServerError, "서버 오류")
            };

            // 에러로그 500은 스택 트레이스까지 남기고, 나머지는 경고로 기록
            if (status == StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "처리되지 않은 예외 발생");
            else
                _logger.LogWarning("{ExceptionType}: {Message}", exception.GetType().Name, exception.Message);

            
            // ProblemDetails는 RFC 7807 표준 오류 형식이라 Swagger 문서화도 자동으로 됨
            // 응답내용 설정
            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,

                // 500일 때 exception.Message를 그대로 내보내면 안 됨 
                // DB 연결 정보, 테이블/컬럼명, 내부 파일 경로 등이 클라이언트에 노출될 수 있음
                // 반면 규칙 위반 메시지는 애초에 사용자에게 보여주려고 쓴 문장이라 그대로 전달
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "요청을 처리하는 중 오류가 발생했습니다."
                    : exception.Message,

                Instance = httpContext.Request.Path
            };

            // 상태코드 설정
            httpContext.Response.StatusCode = status;
            // JSON으로 전송
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

            return true;
        }
    }
}

