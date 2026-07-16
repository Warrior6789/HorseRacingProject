using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
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

    public async Task<PagedResponse<WalletTransactionResponse>> GetPagedAsync(int page, int pageSize, Guid? accountId, string? type)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        WalletTransactionType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<WalletTransactionType>(type, ignoreCase: true, out var t))
            parsedType = t;

        IGenericRepository<WalletTransaction> repo = _uow.GetRepository<WalletTransaction>();

        int totalCount = await repo.Entities
            .Where(w => (accountId == null || w.AccountId == accountId)
                     && (parsedType == null || w.Type == parsedType))
            .CountAsync();

        IEnumerable<WalletTransactionResponse> items = await repo.FindAsync<WalletTransactionResponse>(
            predicate: w => (accountId == null || w.AccountId == accountId)
                          && (parsedType == null || w.Type == parsedType),
            orderBy: q => q.OrderByDescending(w => w.CreatedAt),
            selector: w => new WalletTransactionResponse
            {
                WalletTransactionId = w.WalletTransactionId,
                AccountId           = w.AccountId,
                AccountEmail        = w.Account.Email,
                Type                = w.Type.ToString(),
                Amount              = w.Amount,
                BalanceAfter        = w.BalanceAfter,
                ReferenceId         = w.ReferenceId,
                CreatedAt           = w.CreatedAt
            },
            pageIndex: page - 1,
            pageSize: pageSize
        );

        return new PagedResponse<WalletTransactionResponse>
        {
            Items      = items.ToList(),
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount
        };
    }
}
