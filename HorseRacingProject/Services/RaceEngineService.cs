using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class RaceEngineService : BackgroundService
    {
        private readonly IHubContext<RaceHub> _hubContext;
        private readonly IServiceScopeFactory _factory;
        private readonly Dictionary<Guid, double> _progress = new();
        private readonly Dictionary<Guid, double> _baseSpeeds = new();
        private readonly Dictionary<Guid, double> _momentum = new();
        private readonly Dictionary<Guid, double> _stamina = new();
        private readonly Dictionary<Guid, DateTimeOffset> _finishTimes = new();
        private readonly Random _random = new();

        public RaceEngineService(IHubContext<RaceHub> hubContext, IServiceScopeFactory factory)
        {
            _hubContext = hubContext;
            _factory = factory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _factory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitofWork>();

                DateTimeOffset now = DateTimeOffset.UtcNow;

                List<Race> toOpen = await uow.GetRepository<Race>().Entities
                    .Where(r => r.Status == RaceStatus.Scheduled
                                && r.StartTime <= now.AddMinutes(30)
                                && r.StartTime > now.AddMinutes(5)
                                && !r.IsDeleted)
                    .ToListAsync();

                foreach (Race r in toOpen)
                {
                    r.Status = RaceStatus.BettingOpen;
                    await uow.GetRepository<Race>().UpdateAsync(r);
                }

                List<Race> toClose = await uow.GetRepository<Race>().Entities
                    .Where(r => r.Status == RaceStatus.BettingOpen
                                && r.StartTime <= now.AddMinutes(5)
                                && r.StartTime > now
                                && !r.IsDeleted)
                    .ToListAsync();

                foreach (Race r in toClose)
                {
                    r.Status = RaceStatus.BettingClosed;
                    await uow.GetRepository<Race>().UpdateAsync(r);
                }

                List<Race> toStart = await uow.GetRepository<Race>().Entities
                    .Where(r => r.Status == RaceStatus.BettingClosed
                                && r.StartTime <= now
                                && !r.IsDeleted)
                    .OrderBy(r => r.StartTime)
                    .ToListAsync();

                HashSet<Guid> startedRacecourses = new();
                List<Guid> cancelledRaceIds = new();

                foreach (Race r in toStart)
                {
                    if (startedRacecourses.Contains(r.RacecourseId))
                        continue;

                    bool previousStillRunning = await uow.GetRepository<Race>().Entities
                        .AnyAsync(other => other.RacecourseId == r.RacecourseId
                                        && other.RaceId != r.RaceId
                                        && other.Status == RaceStatus.Live
                                        && !other.IsDeleted);

                    if (previousStillRunning)
                        continue;

                    int confirmedCount = await uow.GetRepository<Registration>().Entities
                        .CountAsync(reg => reg.RaceId == r.RaceId && reg.Status == RegistrationStatus.Confirmed);

                    if (confirmedCount < 2)
                    {
                        DateTimeOffset? previousEndTime = await uow.GetRepository<Race>().Entities
                            .Where(other => other.RacecourseId == r.RacecourseId
                                        && other.RaceId != r.RaceId
                                        && other.Status == RaceStatus.Finished
                                        && !other.IsDeleted)
                            .MaxAsync(other => (DateTimeOffset?)other.EndTime);

                        DateTimeOffset cancelAfter = previousEndTime.HasValue && previousEndTime > r.StartTime
                            ? previousEndTime.Value.AddMinutes(30)
                            : r.StartTime!.Value.AddMinutes(30);

                        if (!previousStillRunning && now > cancelAfter)
                        {
                            r.Status = RaceStatus.Cancelled;
                            cancelledRaceIds.Add(r.RaceId);
                            await uow.GetRepository<Race>().UpdateAsync(r);
                        }
                        continue;
                    }

                    r.Status = RaceStatus.Live;
                    await uow.GetRepository<Race>().UpdateAsync(r);
                    startedRacecourses.Add(r.RacecourseId);
                }

                if (toOpen.Count > 0 || toClose.Count > 0 || toStart.Count > 0 || cancelledRaceIds.Count > 0)
                {
                    await uow.SaveAsync();
                    await _hubContext.Clients.All.SendAsync("RacesUpdated");
                }

                foreach (Guid cancelledId in cancelledRaceIds)
                {
                    await RefundBetsAsync(uow, cancelledId);
                    await RefundRegistrationFeesAsync(uow, cancelledId);
                }

                List<Race> liveRaces = await uow.GetRepository<Race>().Entities
                    .Where(r => r.Status == RaceStatus.Live && !r.IsDeleted)
                    .ToListAsync();

                foreach (Race race in liveRaces)
                {
                    List<Registration> registrations = await uow.GetRepository<Registration>().Entities
                        .Include(r => r.Horse)
                        .Where(r => r.RaceId == race.RaceId && r.Status == RegistrationStatus.Confirmed)
                        .ToListAsync();

                    var horseStates = new List<object>();
                    int finishedCount = 0;

                    foreach (Registration registration in registrations)
                    {
                        if (!_progress.ContainsKey(registration.HorseId))
                        {
                            _progress[registration.HorseId] = 0.0;
                            double wins = registration.Horse.RecordWins ?? 0;
                            double winBonus = Math.Min(wins * 0.0004, 0.005);
                            _baseSpeeds[registration.HorseId] = 0.005 + _random.NextDouble() * 0.012 + winBonus;
                            _stamina[registration.HorseId] = 0.2 + _random.NextDouble() * 0.8;
                            _momentum[registration.HorseId] = 0;
                        }

                        double currentProgress = _progress[registration.HorseId];
                        bool isFinished = currentProgress >= 1.0;

                        double speed = 0;
                        if (!isFinished)
                        {
                            double fatigue = currentProgress * (1.0 - _stamina[registration.HorseId]) * 0.004;

                            if (_random.NextDouble() < 0.08)
                                _momentum[registration.HorseId] = (_random.NextDouble() - 0.6) * 0.008;

                            _momentum[registration.HorseId] *= 0.80;

                            double noise = (_random.NextDouble() - 0.5) * 0.0008;
                            speed = _baseSpeeds[registration.HorseId] - fatigue + _momentum[registration.HorseId] + noise;
                            speed = Math.Max(0.002, Math.Min(0.025, speed));
                            _progress[registration.HorseId] += speed;
                            currentProgress = _progress[registration.HorseId];
                            isFinished = currentProgress >= 1.0;
                        }

                        if (isFinished)
                        {
                            finishedCount++;
                            if (!_finishTimes.ContainsKey(registration.HorseId))
                                _finishTimes[registration.HorseId] = DateTimeOffset.UtcNow;
                        }

                        horseStates.Add(new
                        {
                            id = registration.HorseId,
                            progress = Math.Min(1.0, currentProgress),
                            speed = speed,
                            isFinished = isFinished
                        });
                    }

                    await _hubContext.Clients.Group($"race-{race.RaceId}")
                        .SendAsync("RaceUpdate", new
                        {
                            raceId = race.RaceId,
                            tick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            status = race.Status.ToString(),
                            horses = horseStates
                        });

                    if (finishedCount == registrations.Count && registrations.Count > 0)
                    {
                        race.Status = RaceStatus.Finished;
                        race.EndTime = DateTimeOffset.UtcNow;
                        await uow.GetRepository<Race>().UpdateAsync(race);

                        var sortedRegs = registrations
                            .OrderBy(r => _finishTimes.GetValueOrDefault(r.HorseId, DateTimeOffset.MaxValue))
                            .ToList();

                        for (int i = 0; i < sortedRegs.Count; i++)
                        {
                            Registration reg = sortedRegs[i];
                            int? finishMs = null;
                            if (race.StartTime.HasValue && _finishTimes.TryGetValue(reg.HorseId, out DateTimeOffset ft))
                                finishMs = (int)(ft - race.StartTime.Value).TotalMilliseconds;

                            var result = new RaceResult
                            {
                                ResultId = Guid.NewGuid(),
                                RegistrationId = reg.RegistrationId,
                                FinishPosition = i + 1,
                                FinishTime = finishMs,
                                IsDisqualified = false,
                                CreateAt = DateTimeOffset.UtcNow
                            };
                            await uow.GetRepository<RaceResult>().AddAsync(result);
                        }

                        await uow.SaveAsync();
                        decimal carryover = await SettleBetsAsync(uow, race.RaceId, sortedRegs);
                        await DistributePrizesAsync(uow, race, sortedRegs, carryover);

                        await _hubContext.Clients.Group($"race-{race.RaceId}")
                            .SendAsync("RaceUpdate", new
                            {
                                raceId = race.RaceId,
                                tick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                status = RaceStatus.Finished.ToString(),
                                horses = horseStates
                            });

                        foreach (var reg in registrations)
                        {
                            _progress.Remove(reg.HorseId);
                            _baseSpeeds.Remove(reg.HorseId);
                            _momentum.Remove(reg.HorseId);
                            _stamina.Remove(reg.HorseId);
                            _finishTimes.Remove(reg.HorseId);
                        }

                        await _hubContext.Clients.All.SendAsync("RacesUpdated");
                    }
                }

                int delayMs = liveRaces.Count > 0 ? 100 : 5000;
                await Task.Delay(delayMs, stoppingToken);
            }
        }

        public void ClearHorseState(IEnumerable<Guid> horseIds)
        {
            foreach (Guid id in horseIds)
            {
                _progress.Remove(id);
                _baseSpeeds.Remove(id);
                _momentum.Remove(id);
                _stamina.Remove(id);
                _finishTimes.Remove(id);
            }
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
                    PrizeId = Guid.NewGuid(),
                    RegistrationId = reg.RegistrationId,
                    PrizeType = PrizeType.Owner,
                    Amount = ownerAmount,
                    DistributedAt = DateTimeOffset.UtcNow
                });

                await uow.GetRepository<Prize>().AddAsync(new Prize
                {
                    PrizeId = Guid.NewGuid(),
                    RegistrationId = reg.RegistrationId,
                    PrizeType = PrizeType.Jockey,
                    Amount = jockeyAmount,
                    DistributedAt = DateTimeOffset.UtcNow
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

        private async Task RefundRegistrationFeesAsync(IUnitofWork uow, Guid raceId)
        {
            Race? race = await uow.GetRepository<Race>().Entities
                .FirstOrDefaultAsync(r => r.RaceId == raceId);
            if (race == null || race.RegistrationFee <= 0) return;

            List<Registration> registrations = await uow.GetRepository<Registration>().Entities
                .Include(r => r.Horse)
                .Where(r => r.RaceId == raceId
                    && (r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending))
                .ToListAsync();

            var refunds = new List<(Guid accountId, long amount, long newBalance)>();

            foreach (Registration reg in registrations)
            {
                UserProfile? profile = await uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == reg.Horse.OwnerId && !p.IsDeleted);
                if (profile != null)
                {
                    profile.Balance = (profile.Balance ?? 0) + (long)race.RegistrationFee;
                    profile.UpdatedAt = DateTimeOffset.UtcNow;
                    await uow.GetRepository<UserProfile>().UpdateAsync(profile);
                    refunds.Add((reg.Horse.OwnerId, (long)race.RegistrationFee, profile.Balance ?? 0));
                }
            }

            race.PrizePool = 0;
            await uow.GetRepository<Race>().UpdateAsync(race);
            await uow.SaveAsync();

            foreach (var (accountId, amount, newBalance) in refunds)
                await _hubContext.Clients.All.SendAsync("BalanceUpdated", new
                {
                    accountId,
                    amount,
                    newBalance,
                    reason = "RefundRegistrationFee"
                });
        }

        private async Task RefundBetsAsync(IUnitofWork uow, Guid raceId)
        {
            List<Bet> bets = await uow.GetRepository<Bet>().Entities
                .Include(b => b.Registration)
                .Where(b => b.Registration.RaceId == raceId && b.Status == BetStatus.Active)
                .ToListAsync();

            var refunds = new List<(Guid accountId, long amount, long newBalance)>();

            foreach (Bet bet in bets)
            {
                bet.Status = BetStatus.Refunded;
                await uow.GetRepository<Bet>().UpdateAsync(bet);

                UserProfile? profile = await uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == bet.SpectatorId && !p.IsDeleted);
                if (profile != null)
                {
                    profile.Balance = (profile.Balance ?? 0) + (long)bet.BetAmount;
                    profile.UpdatedAt = DateTimeOffset.UtcNow;
                    await uow.GetRepository<UserProfile>().UpdateAsync(profile);
                    refunds.Add((bet.SpectatorId, (long)bet.BetAmount, profile.Balance ?? 0));
                }
            }

            await uow.SaveAsync();

            foreach (var (accountId, amount, newBalance) in refunds)
                await _hubContext.Clients.All.SendAsync("BalanceUpdated", new
                {
                    accountId,
                    amount,
                    newBalance,
                    reason = "RefundBet"
                });
        }
    }
}
