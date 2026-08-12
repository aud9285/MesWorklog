using MesWorklog.Dtos;
using MesWorklog.Services;
using Microsoft.AspNetCore.Mvc;

namespace MesWorklog.Controllers
{
    [ApiController]
    [Route("api/pause-reason")]
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
