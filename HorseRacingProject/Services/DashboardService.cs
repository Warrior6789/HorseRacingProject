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

var chartTypes = new[]
            {
                WalletTransactionType.Deposit,
                WalletTransactionType.Withdrawal,
                WalletTransactionType.BetPayout,
                WalletTransactionType.PrizePayout
            };

            var chartTransactions = await walletTxQuery
                .Where(w => chartTypes.Contains(w.Type))
                .Select(w => new { w.CreatedAt, w.Amount, w.Type })
                .ToListAsync();

            Func<DateTimeOffset, DateTimeOffset> truncate = bucket?.ToLower() switch
            {
                "hour" => t => new DateTimeOffset(t.Year, t.Month, t.Day, t.Hour, 0, 0, t.Offset),
                "month" => t => new DateTimeOffset(t.Year, t.Month, 1, 0, 0, 0, t.Offset),
                _ => t => new DateTimeOffset(t.Year, t.Month, t.Day, 0, 0, 0, t.Offset)
            };

            var groupedByPeriod = chartTransactions
                .GroupBy(w => truncate(w.CreatedAt))
                .OrderBy(g => g.Key)
                .ToList();

            response.DepositsByPeriod = groupedByPeriod
                .Select(g => new DepositPoint
                {
                    Timestamp = g.Key,
                    Amount = g.Where(w => w.Type == WalletTransactionType.Deposit).Sum(w => (decimal)w.Amount)
                })
                .ToList();

            response.TransactionsByPeriod = groupedByPeriod
                .Select(g => new TransactionPeriodPoint
                {
                    Timestamp = g.Key,
                    Deposit = g.Where(w => w.Type == WalletTransactionType.Deposit).Sum(w => (decimal)w.Amount),
                    Withdrawal = g.Where(w => w.Type == WalletTransactionType.Withdrawal).Sum(w => (decimal)Math.Abs(w.Amount)),
                    BetPayout = g.Where(w => w.Type == WalletTransactionType.BetPayout).Sum(w => (decimal)w.Amount),
                    PrizePayout = g.Where(w => w.Type == WalletTransactionType.PrizePayout).Sum(w => (decimal)w.Amount)
                })
                .ToList();

            return response;
        }

        public async Task<RaceStatusBreakdownResponse> GetRaceStatusBreakdownAsync()
        {
            List<RaceStatusCount> byStatus = await _uow.GetRepository<Race>().Entities
                .Where(r => !r.IsDeleted)
                .GroupBy(r => r.Status)
                .Select(g => new RaceStatusCount { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();

            return new RaceStatusBreakdownResponse
            {
                TotalRaces = byStatus.Sum(s => s.Count),
                ByStatus = byStatus
            };
        }

        public async Task<BetTypeBreakdownResponse> GetBetTypeBreakdownAsync()
        {
            List<BetTypeCount> byType = await _uow.GetRepository<Bet>().Entities
                .Where(b => b.BetType != null)
                .GroupBy(b => b.BetType)
                .Select(g => new BetTypeCount
                {
                    BetType = g.Key!.ToString()!,
                    Count = g.Count(),
                    TotalAmount = g.Sum(b => b.BetAmount)
                })
                .ToListAsync();

            return new BetTypeBreakdownResponse { ByType = byType };
        }
    }
}
