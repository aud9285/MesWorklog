using MesWorklog.Dtos;
using MesWorklog.Services;
using Microsoft.AspNetCore.Mvc;

namespace MesWorklog.Controllers
{

    // [ApiController]: Web API 전용 규약을 켜는 어트리뷰트
    // 자동 400에러 검증, [Frombody], [FromRoute], [FromQuery] 자동 적용
    [ApiController]
    [Route("api/work-orders")]
    public class WorkOrderController : ControllerBase
    {
        private readonly WorkOrderService _workOrderService;

        public WorkOrderController(WorkOrderService workOrderService)
        {
            _workOrderService = workOrderService;
        }

        // 이어하기 목록
        // GET /api/work-orders/open?workerId=1
        [HttpGet("open")]
        public async Task<ActionResult<List<OpenWorkOrderResponse>>> GetOpen( int workerId)
            => Ok(await _workOrderService.GetOpenByWorkerAsync(workerId));


    }
}
