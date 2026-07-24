using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TakeoutConfigController : ControllerBase
    {
        private readonly ITakeoutConfigService _takeoutConfigService;

        public TakeoutConfigController(ITakeoutConfigService takeoutConfigService)
        {
            _takeoutConfigService = takeoutConfigService;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("page")]
        public async Task<IActionResult> GetAllPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<TakeoutConfigResponse> result = await _takeoutConfigService.GetAllPagedAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<TakeoutConfigResponse>>.SuccessResponse(result, "Get takeout configs successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            TakeoutConfigResponse result = await _takeoutConfigService.GetActiveConfigAsync();
            return Ok(ApiResponse<TakeoutConfigResponse>.SuccessResponse(result, "Get active takeout config successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTakeoutConfigRequest req)
        {
            TakeoutConfigResponse result = await _takeoutConfigService.CreateAsync(req);
            return Ok(ApiResponse<TakeoutConfigResponse>.SuccessResponse(result, "Takeout config created successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            await _takeoutConfigService.SetActiveAsync(id);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Takeout config activated successfully."));
        }
    }
}
