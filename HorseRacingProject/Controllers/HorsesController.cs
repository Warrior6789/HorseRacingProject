using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HorseRacingAPI.Controllers
{
    [Route("api/horses")]
    [ApiController]
    [Authorize(Roles = "Admin,HorseOwner")]
    public class HorsesController : ControllerBase
    {
        private readonly IHorseService _horseService;

        public HorsesController(IHorseService horseService)
        {
            _horseService = horseService;
        }

        [HttpGet]
        public async Task<IActionResult> GetHorses([FromQuery] HorseQueryRequest query)
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            PagedResponse<HorseDetailResponse> horses = await _horseService.GetHorsesAsync(accountId, isAdmin, query);
            return Ok(ApiResponse<PagedResponse<HorseDetailResponse>>.SuccessResponse(horses, "Get horses successfully."));
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveHorses()
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            List<HorseResponse> horses = await _horseService.GetActiveHorsesAsync(accountId, isAdmin);
            return Ok(ApiResponse<List<HorseResponse>>.SuccessResponse(horses, "Get active horses successfully."));
        }

        [Authorize(Roles = "HorseOwner")]
        [HttpGet("my-schedule")]
        public async Task<IActionResult> GetMySchedule()
        {
            Guid accountId = GetAccountIdFromToken();
            List<HorseScheduleResponse> schedule = await _horseService.GetMyScheduleAsync(accountId);
            return Ok(ApiResponse<List<HorseScheduleResponse>>.SuccessResponse(schedule, "Get horse schedule successfully."));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetHorseById(Guid id)
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            HorseDetailResponse horse = await _horseService.GetHorseByIdAsync(id, accountId, isAdmin);
            return Ok(ApiResponse<HorseDetailResponse>.SuccessResponse(horse, "Get horse successfully."));
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateHorse([FromForm] HorseCreateRequest request)
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            HorseDetailResponse horse = await _horseService.CreateHorseAsync(accountId, isAdmin, request);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<HorseDetailResponse>.SuccessResponse(horse, "Horse created successfully."));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateHorse(Guid id, [FromBody] HorseUpdateRequest request)
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            HorseDetailResponse horse = await _horseService.UpdateHorseAsync(id, accountId, isAdmin, request);
            return Ok(ApiResponse<HorseDetailResponse>.SuccessResponse(horse, "Horse updated successfully."));
        }

        [HttpPut("{id:guid}/image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadHorseImage(Guid id, IFormFile file)
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            string imageUrl = await _horseService.UploadImageAsync(id, accountId, isAdmin, file);
            return Ok(ApiResponse<string>.SuccessResponse(imageUrl, "Horse image uploaded successfully."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteHorse(Guid id)
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            await _horseService.DeleteHorseAsync(id, accountId, isAdmin);
            return Ok(ApiResponse<object>.SuccessResponse("Horse deleted successfully."));
        }

        [HttpGet("{horseId:guid}/schedule")]
        public async Task<IActionResult> GetHorseSchedule(Guid horseId)
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            List<HorseScheduleResponse> schedule = await _horseService.GetHorseScheduleAsync(horseId, accountId, isAdmin);
            return Ok(ApiResponse<List<HorseScheduleResponse>>.SuccessResponse(schedule, "Get horse schedule successfully."));
        }

        [HttpGet("{horseId:guid}/rewards")]
        public async Task<IActionResult> GetHorseRewards(Guid horseId, [FromQuery] HorseRewardsQueryRequest query)
        {
            Guid accountId = GetAccountIdFromToken();
            bool isAdmin = IsAdmin();

            HorseRewardsResponse rewards = await _horseService.GetHorseRewardsAsync(horseId, accountId, isAdmin, query);
            return Ok(ApiResponse<HorseRewardsResponse>.SuccessResponse(rewards, "Get horse rewards successfully."));
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
