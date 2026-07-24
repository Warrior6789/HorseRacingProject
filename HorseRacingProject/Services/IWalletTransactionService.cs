using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services;

public interface IWalletTransactionService
{
    Task<List<BalanceMismatchResponse>> GetReconciliationAsync();
    Task<PagedResponse<WalletTransactionResponse>> GetPagedAsync(int page, int pageSize, Guid? accountId, string? type);
}
