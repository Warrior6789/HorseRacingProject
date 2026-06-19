using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HorseRacingAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WithdrawalsController : ControllerBase
{
    private readonly IWithdrawalService _withdrawalService;

    public WithdrawalsController(IWithdrawalService withdrawalService)
    {
        _withdrawalService = withdrawalService;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateWithdrawal([FromBody] WithdrawalRequest request)
    {
        Guid accountId = GetAccountId();
        WithdrawalResponse result = await _withdrawalService.CreateWithdrawalAsync(accountId, request);
        return Ok(ApiResponse<WithdrawalResponse>.SuccessResponse(result, "Withdrawal request created successfully."));
    }

    [Authorize]
    [HttpGet("my-history")]
    public async Task<IActionResult> GetMyHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        Guid accountId = GetAccountId();
        PagedResponse<WithdrawalResponse> result = await _withdrawalService.GetMyHistoryAsync(accountId, page, pageSize);
        return Ok(ApiResponse<PagedResponse<WithdrawalResponse>>.SuccessResponse(result));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        PagedResponse<WithdrawalResponse> result = await _withdrawalService.GetPendingAsync(page, pageSize);
        return Ok(ApiResponse<PagedResponse<WithdrawalResponse>>.SuccessResponse(result));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ProcessWithdrawalDto dto)
    {
        WithdrawalResponse result = await _withdrawalService.ApproveAsync(id, dto);
        return Ok(ApiResponse<WithdrawalResponse>.SuccessResponse(result, "Withdrawal approved."));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ProcessWithdrawalDto dto)
    {
        WithdrawalResponse result = await _withdrawalService.RejectAsync(id, dto);
        return Ok(ApiResponse<WithdrawalResponse>.SuccessResponse(result, "Withdrawal rejected. Balance refunded."));
    }

    private Guid GetAccountId()
    {
        string? value = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out Guid accountId))
            throw new UnauthorizedAccessException("Invalid token.");
        return accountId;
    }
}
