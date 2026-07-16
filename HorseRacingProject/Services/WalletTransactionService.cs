using HorseRacingAPI.Dtos;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services;

public class WalletTransactionService : IWalletTransactionService
{
    private readonly IUnitofWork _uow;

    public WalletTransactionService(IUnitofWork uow)
    {
        _uow = uow;
    }

    public async Task<List<BalanceMismatchResponse>> GetReconciliationAsync()
    {
        List<(Guid AccountId, long Balance)> userProfiles = await _uow.GetRepository<UserProfile>().Entities
            .Where(p => !p.IsDeleted)
            .Select(p => new ValueTuple<Guid, long>(p.AccountId, p.Balance ?? 0))
            .ToListAsync();

        List<(Guid AccountId, long Balance)> jockeyProfiles = await _uow.GetRepository<JockeyProfile>().Entities
            .Where(p => !p.IsDeleted)
            .Select(p => new ValueTuple<Guid, long>(p.AccountId, p.Balance ?? 0))
            .ToListAsync();

        Dictionary<Guid, long> ledgerSums = await _uow.GetRepository<WalletTransaction>().Entities
            .GroupBy(w => w.AccountId)
            .Select(g => new { g.Key, Sum = g.Sum(w => w.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum);

        List<BalanceMismatchResponse> mismatches = new();

        void Check(Guid accountId, long currentBalance, string profileType)
        {
            long ledgerBalance = ledgerSums.GetValueOrDefault(accountId, 0);
            if (ledgerBalance != currentBalance)
                mismatches.Add(new BalanceMismatchResponse
                {
                    AccountId = accountId,
                    ProfileType = profileType,
                    CurrentBalance = currentBalance,
                    LedgerBalance = ledgerBalance,
                    Difference = currentBalance - ledgerBalance
                });
        }

        foreach ((Guid accountId, long balance) in userProfiles)
            Check(accountId, balance, "UserProfile");

        foreach ((Guid accountId, long balance) in jockeyProfiles)
            Check(accountId, balance, "JockeyProfile");

        return mismatches;
    }
}
