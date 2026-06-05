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
        public async Task<IActionResult> CreateJockeyProfile([FromBody] JockeyProfileCreateRequest req)
        {
            JockeyProfileResponse response = await _jockeyProfileService.CreateJockeyProfileAsync(req);

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

        [Authorize]
        [HttpGet("{accountId}")]
        public async Task<IActionResult> GetJockeyProfileByAccountId(Guid accountId)
        {
            JockeyProfileResponse response = await _jockeyProfileService.GetJockeyProfileByAccountIdAsync(accountId);

            ApiResponse<JockeyProfileResponse> apiResponse = ApiResponse<JockeyProfileResponse>.SuccessResponse(response, "Get jockey profile details successfully.");
            return Ok(apiResponse);
        }

        [Authorize]
        [HttpPut("{accountId}")]
        public async Task<IActionResult> UpdateJockeyProfile(Guid accountId, [FromBody] JockeyProfileUpdateRequest req)
        {
            string userRole = HttpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (userRole == AccountRole.Jockey.ToString())
            {
                string tokenAccountId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                if (tokenAccountId != accountId.ToString())
                {
                    ApiResponse<object> forbiddenResponse = ApiResponse<object>.FailResponse("You do not have permission to modify this profile.");
                    return StatusCode(StatusCodes.Status403Forbidden, forbiddenResponse);
                }
            }

            await _jockeyProfileService.UpdateJockeyProfileAsync(accountId, req);

            ApiResponse<object> apiResponse = ApiResponse<object>.SuccessResponse(null!, "Update jockey profile successfully.");
            return Ok(apiResponse);
        }
    }
}
