using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HorseRacingAPI.Controllers
{
    [Route("api/dashboard")]
    [ApiController]
    [Authorize(Roles = "Admin,HorseOwner")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("active-runners")]
        public async Task<IActionResult> GetActiveRunners()
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            ActiveRunnersResponse response = await _dashboardService.GetActiveRunnersAsync(accountId, isAdmin);
            return Ok(ApiResponse<ActiveRunnersResponse>.SuccessResponse(response, "Get active runners successfully."));
        }

        [HttpGet("win-rate")]
        public async Task<IActionResult> GetWinRate()
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            WinRateResponse response = await _dashboardService.GetWinRateAsync(accountId, isAdmin);
            return Ok(ApiResponse<WinRateResponse>.SuccessResponse(response, "Get win rate successfully."));
        }

        [HttpGet("recent-rewards")]
        public async Task<IActionResult> GetRecentRewards()
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            RecentRewardsResponse response = await _dashboardService.GetRecentRewardsAsync(accountId, isAdmin);
            return Ok(ApiResponse<RecentRewardsResponse>.SuccessResponse(response, "Get recent rewards successfully."));
        }

        private Guid GetAccountIdFromToken()
        {
            string? value = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(value, out Guid accountId))
                throw new UnauthorizedAccessException("Invalid token.");
            return accountId;
        }

        private bool IsAdmin()
        {
            string role = HttpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            return role == AccountRole.Admin.ToString();
        }
    }
}
