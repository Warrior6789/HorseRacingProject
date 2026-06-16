using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversionRateController : ControllerBase
    {
        private readonly IConversionRateService _conversionRateService;
        public ConversionRateController(IConversionRateService conversionRateService)
        {
            _conversionRateService = conversionRateService;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("page")]
        public async Task<IActionResult> GetAllConversionRatePage([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<ConversionRateResponse> conversionRates = await _conversionRateService.GetAllAsyncPage(page, pageSize);
            return Ok(ApiResponse<PagedResponse<ConversionRateResponse>>.SuccessResponse(conversionRates, "Get conversion rates successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateConversionRate([FromBody] CreateConversionRateRequest req)
        {
            ConversionRateResponse newConversionRate = await _conversionRateService.CreateAsync(req);
            return Ok(ApiResponse<ConversionRateResponse>.SuccessResponse(newConversionRate, "Create conversion rate successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveConversionRate()
        {
            ConversionRateResponse activeConversionRate = await _conversionRateService.GetActiveRateAsync();
            return Ok(ApiResponse<ConversionRateResponse>.SuccessResponse(activeConversionRate, "Get active conversion rate successfully."));

        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/activate")]
        public async Task<IActionResult> SetActiveConversionRate(Guid id)
        {
            await _conversionRateService.SetActiveAsync(id);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Conversion rate activated successfully."));
        }
    }
}
