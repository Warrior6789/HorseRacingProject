using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegistrationFeeConfigController : ControllerBase
    {
        private readonly IRegistrationFeeConfigService _service;

        public RegistrationFeeConfigController(IRegistrationFeeConfigService service)
        {
            _service = service;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("page")]
        public async Task<IActionResult> GetAllPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<RegistrationFeeConfigResponse> result = await _service.GetAllPagedAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<RegistrationFeeConfigResponse>>.SuccessResponse(result, "Get registration fee configs successfully."));
        }

        [Authorize(Roles = "Admin,HorseOwner")]
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            RegistrationFeeConfigResponse result = await _service.GetActiveAsync();
            return Ok(ApiResponse<RegistrationFeeConfigResponse>.SuccessResponse(result, "Get active registration fee config successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRegistrationFeeConfigRequest req)
        {
            RegistrationFeeConfigResponse result = await _service.CreateAsync(req);
            return Ok(ApiResponse<RegistrationFeeConfigResponse>.SuccessResponse(result, "Registration fee config created successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            await _service.SetActiveAsync(id);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Registration fee config activated successfully."));
        }
    }
}
