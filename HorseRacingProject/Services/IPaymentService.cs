using HorseRacingAPI.Dtos;
using PayOS.Models.Webhooks;

namespace HorseRacingAPI.Services;

public interface IPaymentService
{
    Task<string> CreateDepositUrlAsync(Guid accountId, DepositRequest request, string ipAddress);
    Task<PaymentResponse> ProcessWebhookAsync(Webhook webhookBody);
    Task<PagedResponse<PaymentResponse>> GetHistoryPagingAsync(Guid accountId, int page, int pageSize);
}
