using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
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
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (registerDto == null)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Invalid client request."));
            }

            try
            {
                await _authService.RegisterAsync(registerDto);

                return Ok(ApiResponse<object>.SuccessResponse("Registration successful! Please check your status."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.FailResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.FailResponse($"Server error: {ex.Message}"));
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (loginDto == null)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Invalid client request."));
            }

            try
            {
                string? token = await _authService.LoginAsync(loginDto);

                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(ApiResponse<object>.FailResponse("Invalid email or password."));
                }

                var result = new { token = token };
                return Ok(ApiResponse<object>.SuccessResponse(result, "Login successful."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.FailResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.FailResponse($"Server error: {ex.Message}"));
            }
        }
    }
}