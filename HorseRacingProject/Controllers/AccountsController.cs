using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountsController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAccountsByStatus([FromQuery] string status = "Pending")
        {
            List<AccountResponse> accounts = await _accountService.GetAccountByStatusAsync(status);
            return Ok(ApiResponse<List<AccountResponse>>.SuccessResponse(accounts, "Get accounts successfully."));
        }

        [HttpPut("{accountId}/approve")]
        public async Task<IActionResult> ApproveAccount(Guid accountId)
        {
            await _accountService.ApproveAccountAsync(accountId);
            return Ok(ApiResponse<object>.SuccessResponse("Account approved successfully."));
        }

        [HttpPut("{accountId}/suspend")]
        public async Task<IActionResult> SuspendAccount(Guid accountId)
        {
            await _accountService.SuspendAccountAsync(accountId);
            return Ok(ApiResponse<object>.SuccessResponse("Account suspended successfully."));
        }

        [HttpPut("{accountId}/ban")]
        public async Task<IActionResult> BanAccount(Guid accountId)
        {
            await _accountService.BanAccountAsync(accountId);
            return Ok(ApiResponse<object>.SuccessResponse("Account banned successfully."));
        }

        [HttpPut("{accountId}/restore")]
        public async Task<IActionResult> RestoreAccount(Guid accountId)
        {
            await _accountService.RestoreAccountAsync(accountId);
            return Ok(ApiResponse<object>.SuccessResponse("Account restored successfully."));
        }
    }
}
