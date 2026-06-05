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
        public async Task<IActionResult> CreateUserProfile([FromBody] UserProfileCreateRequest req)
        {
            UserProfileResponse response = await _userProfileService.CreateUserProfileAsync(req);

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
            if(userRole == AccountRole.Spectator.ToString() ||
            userRole == AccountRole.Jockey.ToString() ||
            userRole == AccountRole.HorseOwner.ToString())
            {
                string tokenAccountId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                if(tokenAccountId != accountId.ToString())
                {
                    ApiResponse<object> forbiddenResponse = ApiResponse<object>.SuccessResponse(null!, "You do not have permission to modify this profile.");
                    return StatusCode(StatusCodes.Status403Forbidden, forbiddenResponse);
                }
            }
            await _userProfileService.UpdateUserProfileAsync(accountId, req);

            ApiResponse<object> apiResponse = ApiResponse<object>.SuccessResponse(null!, "Update user profile successfully.");
            return Ok(apiResponse);
        }
    }
}