using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
        {
            DashboardSummaryResponse summary = await _dashboardService.GetSummaryAsync(from, to);
            return Ok(ApiResponse<DashboardSummaryResponse>.SuccessResponse(summary, "Get dashboard summary successfully."));
        }
    }
}
