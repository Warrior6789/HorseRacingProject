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
        private readonly IRaceSettlementService _settlementService;
        private readonly Dictionary<Guid, double> _progress = new();
        private readonly Dictionary<Guid, DateTimeOffset> _finishTimes = new();
        private readonly HashSet<Guid> _initializedRaces = new();
        private readonly Dictionary<Guid, int> _raceTick = new();
        private readonly Dictionary<Guid, int> _targetFinishTick = new();
        private readonly Dictionary<Guid, double> _raceStyle = new();
        private readonly Dictionary<Guid, Dictionary<Guid, int>> _pendingOverrides = new();
        private readonly Random _random = new();
        private readonly ILogger<RaceEngineService> _logger;

        public RaceEngineService(IHubContext<RaceHub> hubContext, IServiceScopeFactory factory, IRaceSettlementService settlementService, ILogger<RaceEngineService> logger)
        {
            _hubContext = hubContext;
            _factory = factory;
            _settlementService = settlementService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                int delayMs = 5000;
                try
                {
                using var scope = _factory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitofWork>();

                DateTimeOffset now = DateTimeOffset.UtcNow;

                List<Guid> restingHorseIds = await uow.GetRepository<Horse>().Entities
                    .Where(h => h.Status == HorseStatus.Resting)
                    .Select(h => h.Id)
                    .ToListAsync();

                if (restingHorseIds.Count > 0)
                {
                    bool anyWoken = false;
                    foreach (Guid horseId in restingHorseIds)
                    {
                        DateTimeOffset? lastRaceEnd = await (
                            from r in uow.GetRepository<Registration>().Entities
                            join finishedRace in uow.GetRepository<Race>().Entities on r.RaceId equals finishedRace.RaceId
                            where r.HorseId == horseId
                               && r.Status == RegistrationStatus.Confirmed
                               && finishedRace.Status == RaceStatus.Finished
                            select (DateTimeOffset?)(finishedRace.EndTime ?? finishedRace.StartTime!.Value.AddMinutes(5))
                        ).MaxAsync();

                        if (lastRaceEnd == null || now >= lastRaceEnd.Value.AddDays(7))
                        {
                            Horse? horse = await uow.GetRepository<Horse>().Entities
                                .FirstOrDefaultAsync(h => h.Id == horseId);
                            if (horse != null)
                            {
                                horse.Status = HorseStatus.Healthy;
                                await uow.GetRepository<Horse>().UpdateAsync(horse);
                                anyWoken = true;
                            }
                        }
                    }

                    if (anyWoken)
                        await uow.SaveAsync();
                }

                List<Race> toOpen = await uow.GetRepository<Race>().Entities
                    .Where(r => r.Status == RaceStatus.Scheduled
                                && r.StartTime <= now.AddMinutes(30)
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
                List<Guid> startedRaceIds = new();

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

                    if (confirmedCount < 3)
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
                    startedRaceIds.Add(r.RaceId);
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

                foreach (Guid startedId in startedRaceIds)
                    await AutoRejectPendingRegistrationsAsync(scope, startedId);

                List<Race> liveRaces = await uow.GetRepository<Race>().Entities
                    .Where(r => r.Status == RaceStatus.Live && !r.IsDeleted)
                    .ToListAsync();

                foreach (Race race in liveRaces)
                {
                    List<Registration> registrations = await uow.GetRepository<Registration>().Entities
                        .Include(r => r.Horse)
                        .Where(r => r.RaceId == race.RaceId && r.Status == RegistrationStatus.Confirmed)
                        .ToListAsync();

                    if (!_initializedRaces.Contains(race.RaceId))
                        InitializeRace(race.RaceId, registrations);

                    _raceTick[race.RaceId]++;

                    var horseStates = new List<object>();
                    int finishedCount = 0;

                    foreach (Registration registration in registrations)
                    {
                        double currentProgress = _progress[registration.HorseId];
                        bool isFinished = currentProgress >= 1.0;

                        double speed = 0;
                        if (!isFinished)
                        {
                            int currentTick = _raceTick[race.RaceId];
                            int targetTick = _targetFinishTick[registration.HorseId];
                            double style = _raceStyle[registration.HorseId];

                            double targetProgressNow = Math.Min(1.0, (double)currentTick / targetTick);
                            double progressError = targetProgressNow - currentProgress;
                            double styleEffect = style * (0.5 - currentProgress) * 0.008;
                            double noise = (_random.NextDouble() - 0.5) * 0.0003;

                            speed = progressError * 0.5 + 0.003 + styleEffect + noise;
                            speed = Math.Max(0.001, Math.Min(0.02, speed));
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
                            horseId = registration.HorseId,
                            registrationId = registration.RegistrationId,
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

                        foreach (Registration reg in registrations)
                        {
                            if (reg.Horse.Status == HorseStatus.Healthy)
                            {
                                reg.Horse.Status = HorseStatus.Resting;
                                await uow.GetRepository<Horse>().UpdateAsync(reg.Horse);
                            }
                        }

                        await uow.SaveAsync();
                        await _settlementService.TrySettleAsync(race.RaceId);

                        await _hubContext.Clients.Group($"race-{race.RaceId}")
                            .SendAsync("RaceUpdate", new
                            {
                                raceId = race.RaceId,
                                tick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                status = RaceStatus.Finished.ToString(),
                                horses = horseStates
                            });

                        _initializedRaces.Remove(race.RaceId);
                        _raceTick.Remove(race.RaceId);
                        foreach (var reg in registrations)
                        {
                            _progress.Remove(reg.HorseId);
                            _targetFinishTick.Remove(reg.HorseId);
                            _raceStyle.Remove(reg.HorseId);
                            _finishTimes.Remove(reg.HorseId);
                        }

                        await _hubContext.Clients.All.SendAsync("RacesUpdated");
                    }
                }

                delayMs = liveRaces.Count > 0 ? 100 : 5000;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "RaceEngineService iteration failed; will retry on next tick.");
                }

                await Task.Delay(delayMs, stoppingToken);
            }
        }

        public void ClearRaceState(Guid raceId, IEnumerable<Guid> horseIds)
        {
            _initializedRaces.Remove(raceId);
            _raceTick.Remove(raceId);
            _pendingOverrides.Remove(raceId);
            foreach (Guid id in horseIds)
            {
                _progress.Remove(id);
                _targetFinishTick.Remove(id);
                _raceStyle.Remove(id);
                _finishTimes.Remove(id);
            }
        }

        private void InitializeRace(Guid raceId, List<Registration> registrations)
        {
            _initializedRaces.Add(raceId);
            _raceTick[raceId] = 0;

            List<Registration> ordered;
            if (_pendingOverrides.TryGetValue(raceId, out var pendingRanks))
            {
                ordered = registrations
                    .OrderBy(r => pendingRanks.TryGetValue(r.HorseId, out int rank) ? rank : int.MaxValue)
                    .ThenBy(_ => _random.NextDouble())
                    .ToList();
                _pendingOverrides.Remove(raceId);
            }
            else
            {
                ordered = registrations.OrderBy(_ => _random.NextDouble()).ToList();
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                Guid horseId = ordered[i].HorseId;
                _targetFinishTick[horseId] = 800 + i * 30 + _random.Next(-10, 11);
                _raceStyle[horseId] = _random.NextDouble() * 2 - 1;
                _progress[horseId] = 0.0;
            }
        }



        private async Task RefundRegistrationFeesAsync(IUnitofWork uow, Guid raceId)
        {
            Race? race = await uow.GetRepository<Race>().Entities
                .FirstOrDefaultAsync(r => r.RaceId == raceId);
            if (race == null) return;

            List<Registration> registrations = await uow.GetRepository<Registration>().Entities
                .Include(r => r.Horse)
                .Where(r => r.RaceId == raceId
                    && (r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending))
                .ToListAsync();

            if (registrations.Count == 0) return;

            var refunds = new List<(Guid accountId, long amount, long newBalance)>();

            foreach (Registration reg in registrations)
            {
                if (race.RegistrationFee > 0)
                {
                    UserProfile? profile = await uow.GetRepository<UserProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == reg.Horse.OwnerId && !p.IsDeleted);
                    if (profile != null)
                    {
                        profile.Balance = (profile.Balance ?? 0) + (long)race.RegistrationFee;
                        profile.UpdatedAt = DateTimeOffset.UtcNow;
                        await uow.GetRepository<UserProfile>().UpdateAsync(profile);
                        refunds.Add((reg.Horse.OwnerId, (long)race.RegistrationFee, profile.Balance ?? 0));

                        await uow.GetRepository<WalletTransaction>().AddAsync(new WalletTransaction
                        {
                            WalletTransactionId = Guid.NewGuid(),
                            AccountId = reg.Horse.OwnerId,
                            Type = WalletTransactionType.RegistrationFeeRefund,
                            Amount = (long)race.RegistrationFee,
                            BalanceAfter = profile.Balance ?? 0,
                            ReferenceId = reg.RegistrationId,
                            CreatedAt = DateTimeOffset.UtcNow
                        });
                    }
                }

                reg.Status = RegistrationStatus.Rejected;
                reg.JockeyConfirmation = false;
                reg.UpdatedAt = DateTimeOffset.UtcNow;
                await uow.GetRepository<Registration>().UpdateAsync(reg);
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

            await _hubContext.Clients.All.SendAsync("RegistrationsUpdated");
        }

        private async Task AutoRejectPendingRegistrationsAsync(IServiceScope scope, Guid raceId)
        {
            IUnitofWork uow = scope.ServiceProvider.GetRequiredService<IUnitofWork>();

            List<Guid> pendingRegistrationIds = await uow.GetRepository<Registration>().Entities
                .Where(r => r.RaceId == raceId && r.Status == RegistrationStatus.Pending)
                .Select(r => r.RegistrationId)
                .ToListAsync();

            if (pendingRegistrationIds.Count == 0)
                return;

            IRegistrationService registrationService = scope.ServiceProvider.GetRequiredService<IRegistrationService>();
            foreach (Guid registrationId in pendingRegistrationIds)
                await registrationService.AdminRejectRegistrationAsync(registrationId);
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

                    await uow.GetRepository<WalletTransaction>().AddAsync(new WalletTransaction
                    {
                        WalletTransactionId = Guid.NewGuid(),
                        AccountId = bet.SpectatorId,
                        Type = WalletTransactionType.BetRefund,
                        Amount = (long)bet.BetAmount,
                        BalanceAfter = profile.Balance ?? 0,
                        ReferenceId = bet.BetId,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
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
