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

        public async Task<DashboardSummaryResponse> GetSummaryAsync(DateTimeOffset? from, DateTimeOffset? to)
        {
            var response = new DashboardSummaryResponse();

            response.RacesByStatus = await _uow.GetRepository<Race>().Entities
                .Where(r => !r.IsDeleted)
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count);

            response.AccountsByRole = await _uow.GetRepository<Account>().Entities
                .Where(a => !a.IsDeleted)
                .GroupBy(a => a.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Role.ToString(), x => x.Count);

            response.RegistrationsByStatus = await _uow.GetRepository<Registration>().Entities
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count);

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

            response.RevenueByDay = await takeoutQuery
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new DailyRevenuePoint
                {
                    Date = DateOnly.FromDateTime(g.Key),
                    TakeoutAmount = g.Sum(t => t.TakeoutAmount)
                })
                .OrderBy(p => p.Date)
                .ToListAsync();

            response.TopHorses = await _uow.GetRepository<Horse>().Entities
                .Where(h => !h.IsDeleted && h.RecordWins != null && h.RecordWins > 0)
                .OrderByDescending(h => h.RecordWins)
                .Take(5)
                .Select(h => new TopHorseResponse
                {
                    HorseId = h.Id,
                    HorseName = h.HorseName,
                    RecordWins = h.RecordWins ?? 0
                })
                .ToListAsync();

            return response;
        }
    }
}
