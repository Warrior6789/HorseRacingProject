using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class JockeyRewardConfigController : ControllerBase
    {
        private readonly IJockeyRewardConfigService _service;

        public JockeyRewardConfigController(IJockeyRewardConfigService service)
        {
            _service = service;
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetAllPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<JockeyRewardConfigResponse> result = await _service.GetAllPagedAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<JockeyRewardConfigResponse>>.SuccessResponse(result, "Get jockey reward configs successfully."));
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            JockeyRewardConfigResponse result = await _service.GetActiveAsync();
            return Ok(ApiResponse<JockeyRewardConfigResponse>.SuccessResponse(result, "Get active jockey reward config successfully."));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateJockeyRewardConfigRequest req)
        {
            JockeyRewardConfigResponse result = await _service.CreateAsync(req);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<JockeyRewardConfigResponse>.SuccessResponse(result, "Jockey reward config created successfully."));
        }

        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            await _service.SetActiveAsync(id);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Jockey reward config activated successfully."));
        }
    }
}
