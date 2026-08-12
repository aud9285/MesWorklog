using MesWorklog.Dtos;
using MesWorklog.Services;
using Microsoft.AspNetCore.Mvc;

namespace MesWorklog.Controllers
{
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
        public async Task<ActionResult<List<OpenWorkOrderResponse>>> GetOpen([FromQuery] int workerId)
            => Ok(await _workOrderService.GetOpenByWorkerAsync(workerId));


    }
}
