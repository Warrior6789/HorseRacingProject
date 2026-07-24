using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HorseRacingAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WalletTransactionsController : ControllerBase
{
    private readonly IWalletTransactionService _walletTransactionService;

    public WalletTransactionsController(IWalletTransactionService walletTransactionService)
    {
        _walletTransactionService = walletTransactionService;
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? type = null)
    {
        Guid accountId = GetAccountIdFromToken();
        PagedResponse<WalletTransactionResponse> result = await _walletTransactionService.GetPagedAsync(page, pageSize, accountId, type);
        return Ok(ApiResponse<PagedResponse<WalletTransactionResponse>>.SuccessResponse(result));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("reconciliation")]
    public async Task<IActionResult> GetReconciliation()
    {
        List<BalanceMismatchResponse> result = await _walletTransactionService.GetReconciliationAsync();
        return Ok(ApiResponse<List<BalanceMismatchResponse>>.SuccessResponse(result));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? accountId = null,
        [FromQuery] string? type = null)
    {
        PagedResponse<WalletTransactionResponse> result = await _walletTransactionService.GetPagedAsync(page, pageSize, accountId, type);
        return Ok(ApiResponse<PagedResponse<WalletTransactionResponse>>.SuccessResponse(result));
    }

    private Guid GetAccountIdFromToken()
    {
        string? value = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out Guid accountId))
            throw new UnauthorizedAccessException("Invalid token.");
        return accountId;
    }
}
