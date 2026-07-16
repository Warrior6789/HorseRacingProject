using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services;

public interface IWalletTransactionService
{
    Task<List<BalanceMismatchResponse>> GetReconciliationAsync();
}
