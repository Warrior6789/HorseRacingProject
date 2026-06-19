using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterDto registerDto)
        {
            if (registerDto == null)
                return BadRequest(ApiResponse<object>.FailResponse("Invalid client request."));

            await _authService.RegisterAsync(registerDto);
            return Ok(ApiResponse<object>.SuccessResponse("Registration successful!"));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (loginDto == null)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Invalid client request."));
            }

            string? token = await _authService.LoginAsync(loginDto);

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(ApiResponse<object>.FailResponse("Invalid email or password."));
            }

            var result = new { token = token };
            return Ok(ApiResponse<object>.SuccessResponse(result, "Login successful."));
        }

        [Authorize(Roles = "Spectator")]
        [HttpPost("upgrade")]
        public async Task<IActionResult> RequestRoleUpgrade([FromForm] RequestRoleUpgradeDto request)
        {
            Guid accountId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            await _authService.RequestRoleUpgradeAsync(accountId, request);
            return Ok(ApiResponse<object>.SuccessResponse("Role upgrade request submitted successfully."));
        }

        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(ApiResponse<object>.SuccessResponse("Logout successful."));
        }
    }
}
