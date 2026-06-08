using HorseRacingAPI.Dtos;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class DashboardService : IDashboardService
    {
        private static readonly string[] InactiveRaceStatuses =
        {
            "Cancelled",
            "Completed",
            "Finished"
        };

        private readonly IUnitofWork _uow;

        public DashboardService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<ActiveRunnersResponse> GetActiveRunnersAsync(Guid accountId, bool isAdmin)
        {
            IQueryable<Horse> horseScope = BuildHorseScope(accountId, isAdmin);

            IQueryable<Registration> activeRegistrations = BuildRegistrationScope(accountId, isAdmin)
                .Where(r =>
                    r.Status != "Rejected" &&
                    r.Race.Status != null &&
                    !InactiveRaceStatuses.Contains(r.Race.Status));

            return new ActiveRunnersResponse
            {
                TotalHorses = await horseScope.CountAsync(),
                ActiveRegistrations = await activeRegistrations.CountAsync(),
                ActiveRunners = await activeRegistrations.Select(r => r.HorseId).Distinct().CountAsync()
            };
        }

        public async Task<WinRateResponse> GetWinRateAsync(Guid accountId, bool isAdmin)
        {
            IQueryable<RaceResult> resultScope = _uow.GetRepository<RaceResult>().Entities
                .Where(r => !r.Registration.Horse.IsDeleted);

            if (!isAdmin)
                resultScope = resultScope.Where(r => r.Registration.Horse.OwnerId == accountId);

            int totalRaces = await resultScope.CountAsync(r => r.IsDisqualified != true);
            int totalWins = await resultScope.CountAsync(r => r.IsDisqualified != true && r.FinishPosition == 1);

            return new WinRateResponse
            {
                TotalRaces = totalRaces,
                TotalWins = totalWins,
                WinRate = totalRaces == 0 ? 0 : Math.Round((double)totalWins / totalRaces * 100, 2)
            };
        }

        public async Task<RecentRewardsResponse> GetRecentRewardsAsync(Guid accountId, bool isAdmin)
        {
            IQueryable<Prize> rewardScope = _uow.GetRepository<Prize>().Entities
                .Where(p => !p.Registration.Horse.IsDeleted);

            if (!isAdmin)
                rewardScope = rewardScope.Where(p => p.Registration.Horse.OwnerId == accountId);

            decimal totalRewardAmount = await rewardScope.SumAsync(p => p.Amount ?? 0);
            int rewardCount = await rewardScope.CountAsync();

            List<RecentRewardItemResponse> recentRewards = await rewardScope
                .OrderByDescending(p => p.DistributedAt ?? DateTimeOffset.MinValue)
                .ThenByDescending(p => p.PrizeId)
                .Take(10)
                .Select(p => new RecentRewardItemResponse
                {
                    PrizeId = p.PrizeId,
                    RegistrationId = p.RegistrationId,
                    HorseId = p.Registration.HorseId,
                    HorseName = p.Registration.Horse.HorseName,
                    PrizeType = p.PrizeType,
                    Amount = p.Amount,
                    DistributedAt = p.DistributedAt
                })
                .ToListAsync();

            return new RecentRewardsResponse
            {
                TotalRewardAmount = totalRewardAmount,
                RewardCount = rewardCount,
                RecentRewards = recentRewards
            };
        }

        private IQueryable<Horse> BuildHorseScope(Guid accountId, bool isAdmin)
        {
            IQueryable<Horse> query = _uow.GetRepository<Horse>().Entities
                .Where(h => !h.IsDeleted);

            if (!isAdmin)
                query = query.Where(h => h.OwnerId == accountId);

            return query;
        }

        private IQueryable<Registration> BuildRegistrationScope(Guid accountId, bool isAdmin)
        {
            IQueryable<Registration> query = _uow.GetRepository<Registration>().Entities
                .Where(r => !r.Horse.IsDeleted && !r.Race.IsDeleted);

            if (!isAdmin)
                query = query.Where(r => r.Horse.OwnerId == accountId);

            return query;
        }
    }
}
