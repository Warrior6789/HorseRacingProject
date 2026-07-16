using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    [Authorize(Roles = "Admin")]
    [HttpGet("reconciliation")]
    public async Task<IActionResult> GetReconciliation()
    {
        List<BalanceMismatchResponse> result = await _walletTransactionService.GetReconciliationAsync();
        return Ok(ApiResponse<List<BalanceMismatchResponse>>.SuccessResponse(result));
    }
}
