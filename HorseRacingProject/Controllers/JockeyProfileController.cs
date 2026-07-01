using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JockeyProfilesController : ControllerBase
    {
        private readonly IJockeyProfileService _jockeyProfileService;

        public JockeyProfilesController(IJockeyProfileService jockeyProfileService)
        {
            _jockeyProfileService = jockeyProfileService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateJockeyProfile([FromForm] JockeyProfileCreateRequest req)
        {
            Guid accountId = GetAccountIdFromToken();
            JockeyProfileResponse response = await _jockeyProfileService.CreateJockeyProfileAsync(accountId, req);
            ApiResponse<JockeyProfileResponse> apiResponse = ApiResponse<JockeyProfileResponse>.SuccessResponse(response, "Create jockey profile successfully.");
            return StatusCode(StatusCodes.Status201Created, apiResponse);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAllJockeyProfiles()
        {
            List<JockeyProfileResponse> profiles = await _jockeyProfileService.GetAllJockeyProfilesAsync();
            ApiResponse<List<JockeyProfileResponse>> apiResponse = ApiResponse<List<JockeyProfileResponse>>.SuccessResponse(profiles, "Get all jockey profiles successfully.");
            return Ok(apiResponse);
        }

        [Authorize(Roles = "Admin,HorseOwner")]
        [HttpGet("paged")]
        public async Task<IActionResult> GetAllJockeyProfilesPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<JockeyProfileResponse> result = await _jockeyProfileService.GetAllJockeyProfilesPagedAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<JockeyProfileResponse>>.SuccessResponse(result, "Get all jockey profiles successfully."));
        }

        [Authorize]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyProfile()
        {
            Guid accountId = GetAccountIdFromToken();
            JockeyProfileResponse response = await _jockeyProfileService.GetJockeyProfileByAccountIdAsync(accountId);
            return Ok(ApiResponse<JockeyProfileResponse>.SuccessResponse(response, "Get my profile successfully."));
        }

        [Authorize]
        [HttpGet("{accountId}")]
        public async Task<IActionResult> GetJockeyProfileByAccountId(Guid accountId)
        {
            JockeyProfileResponse response = await _jockeyProfileService.GetJockeyProfileByAccountIdAsync(accountId);
            ApiResponse<JockeyProfileResponse> apiResponse = ApiResponse<JockeyProfileResponse>.SuccessResponse(response, "Get jockey profile details successfully.");
            return Ok(apiResponse);
        }

        [Authorize(Roles = "Jockey")]
        [HttpGet("my/rewards")]
        public async Task<IActionResult> GetMyRewards([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            Guid accountId = GetAccountIdFromToken();
            JockeyRewardsResponse result = await _jockeyProfileService.GetJockeyRewardsAsync(accountId, page, pageSize);
            return Ok(ApiResponse<JockeyRewardsResponse>.SuccessResponse(result, "Get jockey rewards successfully."));
        }

        [Authorize(Roles = "Jockey")]
        [HttpGet("my/race-history")]
        public async Task<IActionResult> GetMyRaceHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            Guid accountId = GetAccountIdFromToken();
            PagedResponse<JockeyRaceHistoryItemResponse> result = await _jockeyProfileService.GetJockeyRaceHistoryAsync(accountId, page, pageSize);
            return Ok(ApiResponse<PagedResponse<JockeyRaceHistoryItemResponse>>.SuccessResponse(result, "Get jockey race history successfully."));
        }

        [Authorize(Roles = "Jockey")]
        [HttpPut("image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            Guid accountId = GetAccountIdFromToken();
            string imageUrl = await _jockeyProfileService.UploadImageAsync(accountId, file);
            ApiResponse<object> apiResponse = ApiResponse<object>.SuccessResponse(new { imageUrl }, "Upload image successfully.");
            return Ok(apiResponse);
        }

        [Authorize]
        [HttpPut("{accountId}")]
        public async Task<IActionResult> UpdateJockeyProfile(Guid accountId, [FromBody] JockeyProfileUpdateRequest req)
        {
            string userRole = HttpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (userRole == AccountRole.Jockey.ToString())
            {
                if (GetAccountIdFromToken() != accountId)
                {
                    ApiResponse<object> forbiddenResponse = ApiResponse<object>.FailResponse("You do not have permission to modify this profile.");
                    return StatusCode(StatusCodes.Status403Forbidden, forbiddenResponse);
                }
            }
            await _jockeyProfileService.UpdateJockeyProfileAsync(accountId, req);
            ApiResponse<object> apiResponse = ApiResponse<object>.SuccessResponse(null!, "Update jockey profile successfully.");
            return Ok(apiResponse);
        }

        private Guid GetAccountIdFromToken()
        {
            string? value = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(value, out Guid accountId))
                throw new UnauthorizedAccessException("Invalid token.");
            return accountId;
        }
    }
}
