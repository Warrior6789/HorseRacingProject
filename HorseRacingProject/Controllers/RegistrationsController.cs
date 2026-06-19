using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegistrationsController : ControllerBase
    {
        private readonly IRegistrationService _registrationService;
        public RegistrationsController(IRegistrationService registrationService)
        {
            _registrationService = registrationService;
        }

        [Authorize(Roles = "Jockey")]
        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyRequests()
        {
            Guid accountId = GetAccountIdFromToken();
            List<RegistrationResponse> result = await _registrationService.GetMyRequestAsync(accountId);
            return Ok(ApiResponse<List<RegistrationResponse>>.SuccessResponse(result, "Get registration requests successfully."));
          }

        [Authorize(Roles = "Jockey")]
        [HttpGet("my-requests/paged")]
        public async Task<IActionResult> GetMyRequestsPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            Guid accountId = GetAccountIdFromToken();
            PagedResponse<RegistrationResponse> result = await _registrationService.GetMyRequestPagedAsync(accountId, page, pageSize);
            return Ok(ApiResponse<PagedResponse<RegistrationResponse>>.SuccessResponse(result, "Get registration requests successfully."));
        }

        [Authorize(Roles = "Jockey")]
        [HttpPut("{registrationId}/accept")]
        public async Task<IActionResult> Accept(Guid registrationId)
        {
            Guid accountId = GetAccountIdFromToken();
            await _registrationService.AcceptRegistrationAsync(registrationId, accountId);
            return Ok(ApiResponse<object>.SuccessResponse("Registration accepted successfully."));
        }

        [Authorize(Roles = "Jockey")]
        [HttpPut("{registrationId}/reject")]
        public async Task<IActionResult> Reject(Guid registrationId)
        {
            Guid accountId = GetAccountIdFromToken();
            await _registrationService.RejectRegistrationAsync(registrationId, accountId);
            return Ok(ApiResponse<object>.SuccessResponse("Registration rejected successfully."));
        }

        [Authorize(Roles = "Admin,Referee")]
        [HttpPut("{registrationId}/scratch")]
        public async Task<IActionResult> Scratch(Guid registrationId)
        {
            await _registrationService.ScratchHorseAsync(registrationId);
            return Ok(ApiResponse<object>.SuccessResponse("Horse scratched and bets refunded successfully."));
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
