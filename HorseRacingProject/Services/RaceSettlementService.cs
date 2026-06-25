using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class RaceSettlementService : IRaceSettlementService
    {
        private readonly IServiceScopeFactory _factory;
        private readonly IHubContext<RaceHub> _hubContext;

        public RaceSettlementService(IServiceScopeFactory factory, IHubContext<RaceHub> hubContext)
        {
            _factory = factory;
            _hubContext = hubContext;
        }

        public async Task TrySettleAsync(Guid raceId)
        {
            using var scope = _factory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitofWork>();

            var race = await uow.GetRepository<Race>().Entities
                .Include(r => r.PositionPrizeConfig)
                .Include(r => r.JockeyRewardConfig)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);
            if (race == null || race.Status != RaceStatus.Finished) return;

            bool hasPending = await uow.GetRepository<RefereeReport>().Entities
                .AnyAsync(r => r.RaceId == raceId && r.Status == RefereeReportStatus.Pending);
            if (hasPending) return;

            bool alreadySettled = !await uow.GetRepository<Bet>().Entities
                .AnyAsync(b => b.Registration.RaceId == raceId && b.Status == BetStatus.Active);
            if (alreadySettled) return;

            List<Registration> sortedRegs = await uow.GetRepository<RaceResult>().Entities
                .Include(r => r.Registration).ThenInclude(r => r.Horse)
                .Where(r => r.Registration.RaceId == raceId && !r.IsDisqualified)
                .OrderBy(r => r.FinishPosition)
                .Select(r => r.Registration)
                .ToListAsync();

            decimal carryover = await SettleBetsAsync(uow, raceId, sortedRegs);
            await DistributePrizesAsync(uow, race, sortedRegs, carryover);
        }

        private async Task<decimal> SettleBetsAsync(IUnitofWork uow, Guid raceId, List<Registration> sortedRegs)
        {
            TakeoutConfig? takeoutConfig = await uow.GetRepository<TakeoutConfig>().Entities
                .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active);
            decimal takeout = (decimal)(takeoutConfig?.TakeoutPercentage ?? 0.20f);

            Dictionary<Guid, int> positions = sortedRegs
                .Select((r, i) => new { r.RegistrationId, Position = i + 1 })
                .ToDictionary(x => x.RegistrationId, x => x.Position);

            List<Bet> bets = await uow.GetRepository<Bet>().Entities
                .Include(b => b.Registration)
                .Where(b => b.Registration.RaceId == raceId && b.Status == BetStatus.Active)
                .ToListAsync();

            decimal carryover = 0;
            var betPayouts = new List<(Guid accountId, long amount, long newBalance)>();

            foreach (BetType betType in new[] { BetType.Win, BetType.Place, BetType.Show })
            {
                List<Bet> typeBets = bets.Where(b => b.BetType == betType).ToList();
                if (!typeBets.Any()) continue;

                decimal totalPool = typeBets.Sum(b => b.BetAmount);
                decimal netPool = totalPool * (1 - takeout);

                HashSet<Guid> winningRegIds = positions
                    .Where(kvp => betType switch
                    {
                        BetType.Win   => kvp.Value == 1,
                        BetType.Place => kvp.Value <= 2,
                        BetType.Show  => kvp.Value <= 3,
                        _             => false
                    })
                    .Select(kvp => kvp.Key)
                    .ToHashSet();

                decimal winningPool = typeBets
                    .Where(b => winningRegIds.Contains(b.RegistrationId))
                    .Sum(b => b.BetAmount);

                if (winningPool <= 0)
                {
                    foreach (Bet bet in typeBets)
                    {
                        bet.Status = BetStatus.Lost;
                        bet.PayoutRatio = 0;
                        await uow.GetRepository<Bet>().UpdateAsync(bet);
                    }
                    carryover += netPool;
                    continue;
                }

                float ratio = (float)(netPool / winningPool);

                foreach (Bet bet in typeBets)
                {
                    bool won = winningRegIds.Contains(bet.RegistrationId);
                    bet.Status = won ? BetStatus.Won : BetStatus.Lost;
                    bet.PayoutRatio = ratio;
                    await uow.GetRepository<Bet>().UpdateAsync(bet);

                    if (won)
                    {
                        long payout = (long)(bet.BetAmount * (decimal)ratio);
                        UserProfile? profile = await uow.GetRepository<UserProfile>().Entities
                            .FirstOrDefaultAsync(p => p.AccountId == bet.SpectatorId && !p.IsDeleted);
                        if (profile != null)
                        {
                            profile.Balance = (profile.Balance ?? 0) + payout;
                            profile.UpdatedAt = DateTimeOffset.UtcNow;
                            await uow.GetRepository<UserProfile>().UpdateAsync(profile);
                            betPayouts.Add((bet.SpectatorId, payout, profile.Balance ?? 0));
                        }
                    }
                }
            }

            await uow.SaveAsync();

            foreach (var (accountId, amount, newBalance) in betPayouts)
                await _hubContext.Clients.All.SendAsync("BalanceUpdated", new
                {
                    accountId,
                    amount,
                    newBalance,
                    reason = "BetPayout"
                });

            return carryover;
        }

        private async Task DistributePrizesAsync(IUnitofWork uow, Race race, List<Registration> sortedRegs, decimal carryover = 0)
        {
            PositionPrizeConfig? posConfig = await uow.GetRepository<PositionPrizeConfig>().Entities
                .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active);
            JockeyRewardConfig? jockeyConfig = await uow.GetRepository<JockeyRewardConfig>().Entities
                .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active);
            if (posConfig == null || jockeyConfig == null) return;

            decimal racePurse = race.PrizePool + carryover;
            if (racePurse <= 0) return;

            race.PositionPrizeConfigId = posConfig.PositionPrizeConfigId;
            race.JockeyRewardConfigId  = jockeyConfig.JockeyRewardConfigId;
            await uow.GetRepository<Race>().UpdateAsync(race);

            double[] allRatios =
            [
                posConfig.Pos1Ratio, posConfig.Pos2Ratio, posConfig.Pos3Ratio,
                posConfig.Pos4Ratio, posConfig.Pos5Ratio, posConfig.Pos6Ratio
            ];
            int finisherCount = Math.Min(sortedRegs.Count, allRatios.Length);
            double[] usedRatios = allRatios.Take(finisherCount).ToArray();
            double ratioSum = usedRatios.Sum();
            if (ratioSum <= 0) return;
            double[] normalizedRatios = usedRatios.Select(r => r / ratioSum).ToArray();
            var prizePayouts = new List<(Guid accountId, long amount, long newBalance, string reason)>();

            for (int i = 0; i < finisherCount; i++)
            {
                Registration reg = sortedRegs[i];
                int position = i + 1;
                decimal positionPrize = racePurse * (decimal)normalizedRatios[i];
                decimal jockeyAmount = positionPrize * (decimal)(position == 1 ? jockeyConfig.WinCut : jockeyConfig.PlaceCut);
                decimal ownerAmount = positionPrize - jockeyAmount;

                await uow.GetRepository<Prize>().AddAsync(new Prize
                {
                    PrizeId        = Guid.NewGuid(),
                    RegistrationId = reg.RegistrationId,
                    PrizeType      = PrizeType.Owner,
                    Amount         = ownerAmount,
                    DistributedAt  = DateTimeOffset.UtcNow
                });

                await uow.GetRepository<Prize>().AddAsync(new Prize
                {
                    PrizeId        = Guid.NewGuid(),
                    RegistrationId = reg.RegistrationId,
                    PrizeType      = PrizeType.Jockey,
                    Amount         = jockeyAmount,
                    DistributedAt  = DateTimeOffset.UtcNow
                });

                UserProfile? ownerProfile = await uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == reg.Horse.OwnerId && !p.IsDeleted);
                if (ownerProfile != null)
                {
                    ownerProfile.Balance = (ownerProfile.Balance ?? 0) + (long)ownerAmount;
                    ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                    await uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);
                    prizePayouts.Add((reg.Horse.OwnerId, (long)ownerAmount, ownerProfile.Balance ?? 0, "PrizePayout"));
                }

                UserProfile? jockeyProfile = await uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == reg.JockeyId && !p.IsDeleted);
                if (jockeyProfile != null)
                {
                    jockeyProfile.Balance = (jockeyProfile.Balance ?? 0) + (long)jockeyAmount;
                    jockeyProfile.UpdatedAt = DateTimeOffset.UtcNow;
                    await uow.GetRepository<UserProfile>().UpdateAsync(jockeyProfile);
                    prizePayouts.Add((reg.JockeyId, (long)jockeyAmount, jockeyProfile.Balance ?? 0, "PrizePayout"));
                }
            }

            await uow.SaveAsync();

            foreach (var (accountId, amount, newBalance, reason) in prizePayouts)
                await _hubContext.Clients.All.SendAsync("BalanceUpdated", new
                {
                    accountId,
                    amount,
                    newBalance,
                    reason
                });
        }
    }
}
