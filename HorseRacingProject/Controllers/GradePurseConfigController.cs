using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class GradePurseConfigController : ControllerBase
    {
        private readonly IGradePurseConfigService _service;

        public GradePurseConfigController(IGradePurseConfigService service)
        {
            _service = service;
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetAllPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<GradePurseConfigResponse> result = await _service.GetAllPagedAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<GradePurseConfigResponse>>.SuccessResponse(result, "Get grade purse configs successfully."));
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            GradePurseConfigResponse result = await _service.GetActiveAsync();
            return Ok(ApiResponse<GradePurseConfigResponse>.SuccessResponse(result, "Get active grade purse config successfully."));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGradePurseConfigRequest req)
        {
            GradePurseConfigResponse result = await _service.CreateAsync(req);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<GradePurseConfigResponse>.SuccessResponse(result, "Grade purse config created successfully."));
        }

        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            await _service.SetActiveAsync(id);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Grade purse config activated successfully."));
        }
    }
}
