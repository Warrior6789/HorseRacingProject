using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services;

public interface IPaymentService
{
    Task<string> CreateDepositUrlAsync(Guid accountId, DepositRequest request, string ipAddress);
    Task<PaymentResponse> ProcessCallbackAsync(IQueryCollection queryParams);
    Task<PagedResponse<PaymentResponse>> GetHistoryPagingAsync(Guid accountId, int page, int pageSize);
}
