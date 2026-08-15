using MesWorklog.Dtos;
using MesWorklog.Services;
using Microsoft.AspNetCore.Mvc;

namespace MesWorklog.Controllers
{

    // [ApiController]: Web API 전용 규약을 켜는 어트리뷰트
    // 자동 400에러 검증, [Frombody], [FromRoute], [FromQuery] 자동 적용
    [ApiController]
    [Route("api/pause-reasons")]
    public class PauseReasonController : ControllerBase
    {
        private readonly PauseReasonService _pauseReasonService;

        public PauseReasonController(PauseReasonService pauseReasonService)
        {
            _pauseReasonService = pauseReasonService;
        }

        // GET /api/pause-reasons
        [HttpGet]
        public async Task<ActionResult<List<PauseReasonResponse>>> GetAll()
        {
            return Ok(await _pauseReasonService.GetAllAsync());
        }

    }
}
