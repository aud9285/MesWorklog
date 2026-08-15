using MesWorklog.Dtos;
using MesWorklog.Services;
using Microsoft.AspNetCore.Mvc;

namespace MesWorklog.Controllers
{

    // [ApiController]: Web API 전용 규약을 켜는 어트리뷰트
    // 자동 400에러 검증, [Frombody], [FromRoute], [FromQuery] 자동 적용
    [ApiController]
    [Route("api/work-logs")]
    public class WorkLogController : ControllerBase
    {
        private readonly WorkLogService _workLogService;
        // 생성자 파라미터로도 추가해서 DI 받아야 함
        private readonly EffectivenessService _effectivenessService;

        public WorkLogController(WorkLogService workLogService, EffectivenessService effectivenessService)
        {
            _workLogService = workLogService;
            _effectivenessService = effectivenessService;
        }

        // 시작하기
        // POST /api/work-logs/start
        [HttpPost("start")]
        public async Task<ActionResult<WorkLogResponse>> Start(CreateWorkLogRequest request)
        {
            var result = await _workLogService.StartAsync(request);
            return Ok(result);
        }

        // 활성 건 조회 — 현장 작업 화면이 작업자를 고르는 순간 호출한다
        // GET /api/work-logs/active?workerId=1
        [HttpGet("active")]
        public async Task<ActionResult<ActiveWorkLogResponse>> GetActive(
            // workerId: 조회할 작업자의 PK (쿼리스트링)
            int workerId)
        {
            var result = await _workLogService.GetActiveByWorkerAsync(workerId);

            // 활성 건이 없는 건 오류가 아니라 정상 상태라 404가 아닌 204를 반환한다.
            // client.js의 request()가 204를 null로 바꿔주므로 화면에서는 activeLog가 null이 된다
            if (result is null) return NoContent();

            return Ok(result);
        }

        // 정지하기
        // POST /api/work-logs/{id}/pause
        [HttpPost("{id:int}/pause")]
        public async Task<ActionResult<WorkLogResponse>> Pause(
            // id: 정지할 작업이력의 PK (URL 경로)
            int id,
            // request: 정지 시각 + 정지사유 id
            PauseWorkLogRequest request)
        {
            var result = await _workLogService.PauseAsync(id, request);
            return Ok(result);
        }

        // 재개하기
        // POST /api/work-logs/{id}/resume
        [HttpPost("{id:int}/resume")]
        public async Task<ActionResult<WorkLogResponse>> Resume(
            // id: 재개할 작업이력의 PK
            int id,
            // request: 재개 시각
            ResumeWorkLogRequest request)
        {
            var result = await _workLogService.ResumeAsync(id, request);
            return Ok(result);
        }

        // 완료하기
        // POST /api/work-logs/{id}/complete
        [HttpPost("{id:int}/complete")]
        public async Task<ActionResult<WorkLogResponse>> Complete(
            // id: 완료할 작업이력의 PK
            int id,
            // request: 종료 시각 + 실적 수량
            CompleteWorkLogRequest request)
        {
            var result = await _workLogService.CompleteAsync(id, request);
            return Ok(result);
        }
        // 이력삭제 - 오입력, 폐기할 데이터
        // DELETE /api/work-logs/{id}
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<DeleteResult>> Delete(int id)
            => Ok(await _workLogService.DeleteAsync(id));

        // 작업지시 목록 - 기간별조회
        // GET /api/work-logs?startDate=2026-08-01&endDate=2026-08-09
        [HttpGet]
        public async Task<ActionResult<List<WorkLogListItemResponse>>> GetByDateRange(
            DateOnly startDate, DateOnly endDate)
            => Ok(await _workLogService.GetByDateRangeAsync(startDate, endDate));

        // 한 작업지시 작업이력 모아보기
        // GET /api/work-logs/by-order/{workOrderId}
        [HttpGet("by-order/{workOrderId:int}")]
        public async Task<ActionResult<List<WorkLogDetailResponse>>> GetByWorkOrder(int workOrderId)
            => Ok(await _workLogService.GetByWorkOrderIdAsync(workOrderId));



        // [HttpGet("effectiveness")] = /api/work-logs 뒤에 /effectiveness 붙는 라우트
        // GET /api/work-logs/effectiveness?period=day&date=2026-08-15&groupBy=worker
        // period = 기간단위, date = 날짜, groupby = 어떤 그룹의 가동율인지
        [HttpGet("effectiveness")]
        public async Task<ActionResult<List<EffectivenessResponse>>> GetEffectiveness(
            string period, DateTime date, string groupBy)
            => Ok(await _effectivenessService.GetEffectivenessAsync(period, date, groupBy)); // => 식 본문(expression-bodied member), {return ...;}를 줄인 문법
    }
}
