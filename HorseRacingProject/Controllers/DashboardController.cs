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

        [HttpGet("financial")]
        public async Task<IActionResult> GetFinancial([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string bucket = "day")
        {
            DashboardFinancialResponse financial = await _dashboardService.GetFinancialAsync(from, to, bucket);
            return Ok(ApiResponse<DashboardFinancialResponse>.SuccessResponse(financial, "Get dashboard financial summary successfully."));
        }

        [HttpGet("races-by-status")]
        public async Task<IActionResult> GetRaceStatusBreakdown()
        {
            RaceStatusBreakdownResponse breakdown = await _dashboardService.GetRaceStatusBreakdownAsync();
            return Ok(ApiResponse<RaceStatusBreakdownResponse>.SuccessResponse(breakdown, "Get race status breakdown successfully."));
        }

        [HttpGet("bets-by-type")]
        public async Task<IActionResult> GetBetTypeBreakdown()
        {
            BetTypeBreakdownResponse breakdown = await _dashboardService.GetBetTypeBreakdownAsync();
            return Ok(ApiResponse<BetTypeBreakdownResponse>.SuccessResponse(breakdown, "Get bet type breakdown successfully."));
        }

        [HttpGet("top-horses")]
        public async Task<IActionResult> GetTopHorses([FromQuery] int limit = 10)
        {
            TopHorsesResponse topHorses = await _dashboardService.GetTopHorsesAsync(limit);
            return Ok(ApiResponse<TopHorsesResponse>.SuccessResponse(topHorses, "Get top horses successfully."));
        }

        [HttpGet("signups")]
        public async Task<IActionResult> GetSignups([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string bucket = "day")
        {
            SignupsResponse signups = await _dashboardService.GetSignupsAsync(from, to, bucket);
            return Ok(ApiResponse<SignupsResponse>.SuccessResponse(signups, "Get signups by period successfully."));
        }
    }
}
