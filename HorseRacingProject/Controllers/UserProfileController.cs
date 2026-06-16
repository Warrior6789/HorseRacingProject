using Guid = System.Guid;
using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using HorseRacingAPI.Enums;
using System.Security.Claims;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserProfilesController : ControllerBase
    {
        private readonly IUserProfileService _userProfileService;

        public UserProfilesController(IUserProfileService userProfileService)
        {
            _userProfileService = userProfileService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateUserProfile([FromForm] UserProfileCreateRequest req)
        {
            Guid accountId = GetAccountIdFromToken();
            UserProfileResponse response = await _userProfileService.CreateUserProfileAsync(accountId, req);
            ApiResponse<UserProfileResponse> apiResponse = ApiResponse<UserProfileResponse>.SuccessResponse(response, "Create user profile successfully.");
            return StatusCode(StatusCodes.Status201Created, apiResponse);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAllUserProfiles()
        {
            List<UserProfileResponse> profiles = await _userProfileService.GetAllUserProfilesAsync();
            ApiResponse<List<UserProfileResponse>> apiResponse = ApiResponse<List<UserProfileResponse>>.SuccessResponse(profiles, "Get all user profiles successfully.");
            return Ok(apiResponse);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("paged")]
        public async Task<IActionResult> GetAllUserProfilesPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<UserProfileResponse> result = await _userProfileService.GetAllUserProfilesPagedAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<UserProfileResponse>>.SuccessResponse(result, "Get all user profiles successfully."));
        }

        [Authorize]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyProfile()
        {
            Guid accountId = GetAccountIdFromToken();
            UserProfileResponse response = await _userProfileService.GetUserProfileByIdAsync(accountId);
            return Ok(ApiResponse<UserProfileResponse>.SuccessResponse(response, "Get my profile successfully."));
        }

        [Authorize]
        [HttpGet("{accountId}")]
        public async Task<IActionResult> GetUserProfileById(Guid accountId)
        {
            UserProfileResponse response = await _userProfileService.GetUserProfileByIdAsync(accountId);
            ApiResponse<UserProfileResponse> apiResponse = ApiResponse<UserProfileResponse>.SuccessResponse(response, "Get user profile details successfully.");
            return Ok(apiResponse);
        }

        [Authorize]
        [HttpPut("{accountId}")]
        public async Task<IActionResult> UpdateUserProfile(Guid accountId, [FromBody] UserProfileUpdateRequest req)
        {
            string userRole = HttpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (userRole == AccountRole.Spectator.ToString() ||
                userRole == AccountRole.Jockey.ToString() ||
                userRole == AccountRole.HorseOwner.ToString())
            {
                if (GetAccountIdFromToken() != accountId)
                {
                    ApiResponse<object> forbiddenResponse = ApiResponse<object>.FailResponse("You do not have permission to modify this profile.");
                    return StatusCode(StatusCodes.Status403Forbidden, forbiddenResponse);
                }
            }
            await _userProfileService.UpdateUserProfileAsync(accountId, req);
            ApiResponse<object> apiResponse = ApiResponse<object>.SuccessResponse(null!, "Update user profile successfully.");
            return Ok(apiResponse);
        }

        [Authorize]
        [HttpPut("image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            Guid accountId = GetAccountIdFromToken();
            string imageUrl = await _userProfileService.UploadImageAsync(accountId, file);
            ApiResponse<object> apiResponse = ApiResponse<object>.SuccessResponse(new { imageUrl }, "Upload image successfully.");
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
