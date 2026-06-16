using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HorseRacingAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [Authorize]
    [HttpPost("deposit")]
    public async Task<IActionResult> CreateDeposit([FromBody] DepositRequest request)
    {
        Guid accountId = GetAccountIdFromToken();
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        string url = await _paymentService.CreateDepositUrlAsync(accountId, request, ipAddress);
        return Ok(ApiResponse<string>.SuccessResponse(url, "Payment URL created successfully."));
    }

    [HttpGet("deposit/callback")]
    public async Task<IActionResult> DepositCallback()
    {
        PaymentResponse result = await _paymentService.ProcessCallbackAsync(Request.Query);
        return Ok(ApiResponse<PaymentResponse>.SuccessResponse(result, "Payment processed successfully."));
    }

    [Authorize]
    [HttpGet("history/paged")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        Guid accountId = GetAccountIdFromToken();
        PagedResponse<PaymentResponse> result = await _paymentService.GetHistoryPagingAsync(accountId, page, pageSize);
        return Ok(ApiResponse<PagedResponse<PaymentResponse>>.SuccessResponse(result, "Get payment history successfully."));
    }

    private Guid GetAccountIdFromToken()
    {
        string? value = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out Guid accountId))
            throw new UnauthorizedAccessException("Invalid token.");
        return accountId;
    }
}
