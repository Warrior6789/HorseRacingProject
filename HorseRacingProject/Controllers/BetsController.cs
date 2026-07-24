using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BetsController : ControllerBase
    {
        private readonly IBetService _betService;

        public BetsController(IBetService betService)
        {
            _betService = betService;
        }

        [Authorize(Roles = "Spectator")]
        [HttpPost]
        public async Task<IActionResult> PlaceBet([FromBody] PlaceBetRequest req)
        {
            Guid spectatorId = GetAccountIdFromToken();
            BetResponse result = await _betService.PlaceBetAsync(spectatorId, req);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<BetResponse>.SuccessResponse(result, "Bet placed successfully."));
        }


        [Authorize]
        [HttpGet("my/paged")]
        public async Task<IActionResult> GetMyBetsPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null)
        {
            Guid spectatorId = GetAccountIdFromToken();
            PagedResponse<BetResponse> result = await _betService.GetMyBetsPagedAsync(spectatorId, page, pageSize, status);
            return Ok(ApiResponse<PagedResponse<BetResponse>>.SuccessResponse(result, "Get bets successfully."));
        }

        private Guid GetAccountIdFromToken()
        {
            string? value = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(value, out Guid id))
                throw new UnauthorizedAccessException("Invalid token.");
            return id;
        }
    }
}
