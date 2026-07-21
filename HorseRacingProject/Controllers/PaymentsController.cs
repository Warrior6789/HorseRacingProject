using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayOS.Models.Webhooks;
using System.Security.Claims;
using System.Text.Json;

namespace HorseRacingAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
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

    [HttpGet("cancel")]
    public async Task<IActionResult> Cancel([FromQuery] long orderCode)
    {
        await _paymentService.CancelPaymentAsync(orderCode);
        return Ok(ApiResponse<string>.SuccessResponse("Cancelled", "Payment cancelled."));
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        string body = string.Empty;
        try
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            body = await reader.ReadToEndAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var webhookBody = JsonSerializer.Deserialize<Webhook>(body, options);
            if (webhookBody == null)
            {
                _logger.LogWarning("PayOS webhook: could not deserialize body: {Body}", body);
            }
            else
            {
                await _paymentService.ProcessWebhookAsync(webhookBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayOS webhook processing failed. Body: {Body}", body);
        }
        return Ok(new { success = true });
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

    [Authorize(Roles = "Admin")]
    [HttpGet("paged")]
    public async Task<IActionResult> GetAllPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        PagedResponse<PaymentResponse> result = await _paymentService.GetAllPaymentsPagingAsync(page, pageSize);
        return Ok(ApiResponse<PagedResponse<PaymentResponse>>.SuccessResponse(result, "Get all payments successfully."));
    }

    private Guid GetAccountIdFromToken()
    {
        string? value = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out Guid accountId))
            throw new UnauthorizedAccessException("Invalid token.");
        return accountId;
    }
}
