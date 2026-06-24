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
        public async Task<IActionResult> GetAccountsByStatus([FromQuery] string status = "Active")
        {
            List<AccountResponse> accounts = await _accountService.GetAccountByStatusAsync(status);
            return Ok(ApiResponse<List<AccountResponse>>.SuccessResponse(accounts, "Get accounts successfully."));
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetAccountsByStatusPaged([FromQuery] string status = "Active", [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<AccountResponse> accounts = await _accountService.GetAccountByStatusPagedAsync(status, page, pageSize);
            return Ok(ApiResponse<PagedResponse<AccountResponse>>.SuccessResponse(accounts, "Get accounts successfully."));
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

        [HttpGet("{accountId}/upgrade-detail")]
        public async Task<IActionResult> GetUpgradeRequestDetail(Guid accountId)
        {
            UpgradeRequestResponse result = await _accountService.GetUpgradeRequestDetailAsync(accountId);
            return Ok(ApiResponse<UpgradeRequestResponse>.SuccessResponse(result, "Get upgrade request detail successfully."));
        }

        [HttpGet("upgrades")]
        public async Task<IActionResult> GetRoleUpgradeRequests()
        {
            List<UpgradeRequestResponse> result = await _accountService.GetRoleUpgradeRequestsAsync();
            return Ok(ApiResponse<List<UpgradeRequestResponse>>.SuccessResponse(result, "Get upgrade requests successfully."));
        }

        [HttpGet("upgrades/paged")]
        public async Task<IActionResult> GetRoleUpgradeRequestsPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<UpgradeRequestResponse> result = await _accountService.GetRoleUpgradeRequestsPagedAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<UpgradeRequestResponse>>.SuccessResponse(result, "Get upgrade requests successfully."));
        }

        [HttpPut("{accountId}/approve-upgrade")]
        public async Task<IActionResult> ApproveRoleUpgrade(Guid accountId)
        {
            await _accountService.ApproveRoleUpgradeAsync(accountId);
            return Ok(ApiResponse<object>.SuccessResponse("Upgrade approved successfully."));
        }

        [HttpPut("{accountId}/reject-upgrade")]
        public async Task<IActionResult> RejectRoleUpgrade(Guid accountId)
        {
            await _accountService.RejectRoleUpgradeAsync(accountId);
            return Ok(ApiResponse<object>.SuccessResponse("Upgrade rejected successfully."));
        }
    }
}
