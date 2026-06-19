using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class PositionPrizeConfigController : ControllerBase
    {
        private readonly IPositionPrizeConfigService _service;

        public PositionPrizeConfigController(IPositionPrizeConfigService service)
        {
            _service = service;
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetAllPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<PositionPrizeConfigResponse> result = await _service.GetAllPagedAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<PositionPrizeConfigResponse>>.SuccessResponse(result, "Get position prize configs successfully."));
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            PositionPrizeConfigResponse result = await _service.GetActiveAsync();
            return Ok(ApiResponse<PositionPrizeConfigResponse>.SuccessResponse(result, "Get active position prize config successfully."));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePositionPrizeConfigRequest req)
        {
            PositionPrizeConfigResponse result = await _service.CreateAsync(req);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<PositionPrizeConfigResponse>.SuccessResponse(result, "Position prize config created successfully."));
        }

        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            await _service.SetActiveAsync(id);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Position prize config activated successfully."));
        }
    }
}
