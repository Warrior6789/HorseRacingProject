using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BetPayoutConfigController : ControllerBase
    {
        private readonly IBetPayoutConfigService _betPayoutConfigService;

        public BetPayoutConfigController(IBetPayoutConfigService betPayoutConfigService)
        {
            _betPayoutConfigService = betPayoutConfigService;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("page")]
        public async Task<IActionResult> GetAllPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<BetPayoutConfigResponse> result = await _betPayoutConfigService.GetAllPagedAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<BetPayoutConfigResponse>>.SuccessResponse(result, "Get bet payout configs successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            BetPayoutConfigResponse result = await _betPayoutConfigService.GetActiveConfigAsync();
            return Ok(ApiResponse<BetPayoutConfigResponse>.SuccessResponse(result, "Get active bet payout config successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBetPayoutConfigRequest req)
        {
            BetPayoutConfigResponse result = await _betPayoutConfigService.CreateAsync(req);
            return Ok(ApiResponse<BetPayoutConfigResponse>.SuccessResponse(result, "Bet payout config created successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            await _betPayoutConfigService.SetActiveAsync(id);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Bet payout config activated successfully."));
        }
    }
}
