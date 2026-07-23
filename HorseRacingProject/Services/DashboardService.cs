using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IUnitofWork _uow;

        public DashboardService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<DashboardFinancialResponse> GetFinancialAsync(DateTimeOffset? from, DateTimeOffset? to, string bucket)
        {
            var response = new DashboardFinancialResponse();

            IQueryable<WalletTransaction> walletTxQuery = _uow.GetRepository<WalletTransaction>().Entities;
            if (from != null) walletTxQuery = walletTxQuery.Where(w => w.CreatedAt >= from);
            if (to != null) walletTxQuery = walletTxQuery.Where(w => w.CreatedAt < to);

            response.Financial = new FinancialSummaryResponse
            {
                TotalDeposits = await walletTxQuery.Where(w => w.Type == WalletTransactionType.Deposit).SumAsync(w => (decimal)w.Amount),
                TotalWithdrawals = await walletTxQuery.Where(w => w.Type == WalletTransactionType.Withdrawal).SumAsync(w => (decimal)Math.Abs(w.Amount)),
                TotalBetsPlaced = await walletTxQuery.Where(w => w.Type == WalletTransactionType.BetPlaced).SumAsync(w => (decimal)Math.Abs(w.Amount)),
                TotalBetPayouts = await walletTxQuery.Where(w => w.Type == WalletTransactionType.BetPayout).SumAsync(w => (decimal)w.Amount),
                TotalPrizePayouts = await walletTxQuery.Where(w => w.Type == WalletTransactionType.PrizePayout).SumAsync(w => (decimal)w.Amount),
            };

            IQueryable<TakeoutLedger> takeoutQuery = _uow.GetRepository<TakeoutLedger>().Entities;
            if (from != null) takeoutQuery = takeoutQuery.Where(t => t.CreatedAt >= from);
            if (to != null) takeoutQuery = takeoutQuery.Where(t => t.CreatedAt < to);

            response.Financial.TotalTakeoutRevenue = await takeoutQuery.SumAsync(t => t.TakeoutAmount);

var deposits = await walletTxQuery
                .Where(w => w.Type == WalletTransactionType.Deposit)
                .Select(w => new { w.CreatedAt, w.Amount })
                .ToListAsync();

            Func<DateTimeOffset, DateTimeOffset> truncate = bucket?.ToLower() switch
            {
                "hour" => t => new DateTimeOffset(t.Year, t.Month, t.Day, t.Hour, 0, 0, t.Offset),
                "month" => t => new DateTimeOffset(t.Year, t.Month, 1, 0, 0, 0, t.Offset),
                _ => t => new DateTimeOffset(t.Year, t.Month, t.Day, 0, 0, 0, t.Offset)
            };

            response.DepositsByPeriod = deposits
                .GroupBy(w => truncate(w.CreatedAt))
                .Select(g => new DepositPoint { Timestamp = g.Key, Amount = g.Sum(w => (decimal)w.Amount) })
                .OrderBy(p => p.Timestamp)
                .ToList();

            return response;
        }
    }
}
