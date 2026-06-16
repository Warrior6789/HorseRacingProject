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
        private readonly Dictionary<Guid, double> _angles = new();
        private readonly Dictionary<Guid, double> _baseSpeeds = new();
        private readonly Dictionary<Guid, double> _momentum = new();
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
                    .Where(r => r.Status == "Scheduled"
                                && r.StartTime <= now.AddMinutes(30)
                                && r.StartTime > now.AddMinutes(5)
                                && !r.IsDeleted)
                    .ToListAsync();

                foreach (Race r in toOpen)
                {
                    r.Status = "BettingOpen";
                    await uow.GetRepository<Race>().UpdateAsync(r);
                }

                List<Race> toClose = await uow.GetRepository<Race>().Entities
                    .Where(r => r.Status == "BettingOpen"
                                && r.StartTime <= now.AddMinutes(5)
                                && r.StartTime > now
                                && !r.IsDeleted)
                    .ToListAsync();

                foreach (Race r in toClose)
                {
                    r.Status = "BettingClosed";
                    await uow.GetRepository<Race>().UpdateAsync(r);
                }

                List<Race> toStart = await uow.GetRepository<Race>().Entities
                    .Where(r => r.Status == "BettingClosed"
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
                                        && other.Status == "Live"
                                        && !other.IsDeleted);

                    if (previousStillRunning)
                        continue;

                    int confirmedCount = await uow.GetRepository<Registration>().Entities
                        .CountAsync(reg => reg.RaceId == r.RaceId && reg.Status == "Confirmed");

                    if (r.MaxParticipants.HasValue && confirmedCount < r.MaxParticipants.Value)
                    {
                        DateTimeOffset? previousEndTime = await uow.GetRepository<Race>().Entities
                            .Where(other => other.RacecourseId == r.RacecourseId
                                        && other.RaceId != r.RaceId
                                        && other.Status == "Finished"
                                        && !other.IsDeleted)
                            .MaxAsync(other => (DateTimeOffset?)other.EndTime);

                        DateTimeOffset cancelAfter = previousEndTime.HasValue && previousEndTime > r.StartTime
                            ? previousEndTime.Value.AddMinutes(30)
                            : r.StartTime!.Value.AddMinutes(30);

                        if (now > cancelAfter)
                        {
                            r.Status = "Cancelled";
                            cancelledRaceIds.Add(r.RaceId);
                            await uow.GetRepository<Race>().UpdateAsync(r);
                        }
                        continue;
                    }

                    r.Status = "Live";
                    await uow.GetRepository<Race>().UpdateAsync(r);
                    startedRacecourses.Add(r.RacecourseId);
                }

                if (toOpen.Count > 0 || toClose.Count > 0 || toStart.Count > 0)
                    await uow.SaveAsync();

                foreach (Guid cancelledId in cancelledRaceIds)
                    await RefundBetsAsync(uow, cancelledId);

                List<Race> liveRaces = await uow.GetRepository<Race>().Entities
                    .Where(r => r.Status == "Live" && !r.IsDeleted)
                    .ToListAsync();

                foreach (Race race in liveRaces)
                {
                    List<Registration> registrations = await uow.GetRepository<Registration>().Entities
                        .Include(r => r.Horse)
                        .Where(r => r.RaceId == race.RaceId && r.Status == "Confirmed")
                        .ToListAsync();

                    var horseStates = new List<object>();
                    int finishedCount = 0;
                    int position = 1;

                    foreach (Registration registration in registrations)
                    {
                        if (!_angles.ContainsKey(registration.HorseId))
                        {
                            _angles[registration.HorseId] = Math.PI;
                            _baseSpeeds[registration.HorseId] = 0.18 + _random.NextDouble() * 0.04;
                            _momentum[registration.HorseId] = 0;
                        }

                        double currentAngle = _angles[registration.HorseId];
                        bool isFinished = currentAngle >= Math.PI + 2 * 2 * Math.PI;

                        double speed = 0;
                        if (!isFinished)
                        {
                            if (_random.NextDouble() < 0.03)
                                _momentum[registration.HorseId] = (_random.NextDouble() - 0.5) * 0.08;

                            double noise = (_random.NextDouble() - 0.5) * 0.02;
                            speed = _baseSpeeds[registration.HorseId] + _momentum[registration.HorseId] + noise;
                            speed = Math.Max(0.12, Math.Min(0.28, speed));
                            _angles[registration.HorseId] += speed * 0.1;
                            currentAngle = _angles[registration.HorseId];
                            isFinished = currentAngle >= Math.PI + 2 * 2 * Math.PI;
                        }

                        int lap = (int)((currentAngle - Math.PI) / (2 * Math.PI));

                        if (isFinished) finishedCount++;

                        horseStates.Add(new
                        {
                            id = registration.HorseId,
                            angle = currentAngle,
                            speed = speed,
                            lap = lap,
                            isFinished = isFinished
                        });
                    }

                    await _hubContext.Clients.Group($"race-{race.RaceId}")
                        .SendAsync("RaceUpdate", new
                        {
                            raceId = race.RaceId,
                            tick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            status = race.Status,
                            horses = horseStates
                        });

                    if (finishedCount == registrations.Count && registrations.Count > 0)
                    {
                        race.Status = "Finished";
                        race.EndTime = DateTimeOffset.UtcNow;
                        await uow.GetRepository<Race>().UpdateAsync(race);

                        var sortedRegs = registrations
                            .OrderByDescending(r => _angles.GetValueOrDefault(r.HorseId, Math.PI))
                            .ToList();

                        for (int i = 0; i < sortedRegs.Count; i++)
                        {
                            var result = new RaceResult
                            {
                                ResultId = Guid.NewGuid(),
                                RegistrationId = sortedRegs[i].RegistrationId,
                                FinishPosition = i + 1,
                                CreateAt = DateTimeOffset.UtcNow
                            };
                            await uow.GetRepository<RaceResult>().AddAsync(result);
                        }

                        await uow.SaveAsync();
                        await SettleBetsAsync(uow, race.RaceId, sortedRegs);

                        await _hubContext.Clients.Group($"race-{race.RaceId}")
                            .SendAsync("RaceUpdate", new
                            {
                                raceId = race.RaceId,
                                tick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                status = "Finished",
                                horses = horseStates
                            });

                        foreach (var reg in registrations)
                        {
                            _angles.Remove(reg.HorseId);
                            _baseSpeeds.Remove(reg.HorseId);
                            _momentum.Remove(reg.HorseId);
                        }
                    }
                }

                await Task.Delay(100, stoppingToken);
            }
        }

        public void ClearHorseState(IEnumerable<Guid> horseIds)
        {
            foreach (Guid id in horseIds)
            {
                _angles.Remove(id);
                _baseSpeeds.Remove(id);
                _momentum.Remove(id);
            }
        }

        private async Task SettleBetsAsync(IUnitofWork uow, Guid raceId, List<Registration> sortedRegs)
        {
            Dictionary<Guid, int> positions = sortedRegs
                .Select((r, i) => new { r.RegistrationId, Position = i + 1 })
                .ToDictionary(x => x.RegistrationId, x => x.Position);

            List<Bet> bets = await uow.GetRepository<Bet>().Entities
                .Include(b => b.Registration)
                .Where(b => b.Registration.RaceId == raceId && b.Status == "Pending")
                .ToListAsync();

            foreach (Bet bet in bets)
            {
                if (!positions.TryGetValue(bet.RegistrationId, out int pos))
                {
                    bet.Status = "Lost";
                    await uow.GetRepository<Bet>().UpdateAsync(bet);
                    continue;
                }

                bool won = bet.BetType switch
                {
                    "Win"   => pos == 1,
                    "Place" => pos <= 2,
                    "Show"  => pos <= 3,
                    _       => false
                };

                bet.Status = won ? "Won" : "Lost";
                await uow.GetRepository<Bet>().UpdateAsync(bet);

                if (won)
                {
                    long payout = (long)(bet.BetAmount * (decimal)(bet.PayoutRatio ?? 1));
                    UserProfile? profile = await uow.GetRepository<UserProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == bet.SpectatorId && !p.IsDeleted);
                    if (profile != null)
                    {
                        profile.Balance = (profile.Balance ?? 0) + payout;
                        profile.UpdatedAt = DateTimeOffset.UtcNow;
                        await uow.GetRepository<UserProfile>().UpdateAsync(profile);
                    }
                }
            }

            await uow.SaveAsync();
        }

        private async Task RefundBetsAsync(IUnitofWork uow, Guid raceId)
        {
            List<Bet> bets = await uow.GetRepository<Bet>().Entities
                .Include(b => b.Registration)
                .Where(b => b.Registration.RaceId == raceId && b.Status == "Pending")
                .ToListAsync();

            foreach (Bet bet in bets)
            {
                bet.Status = "Refunded";
                await uow.GetRepository<Bet>().UpdateAsync(bet);

                UserProfile? profile = await uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == bet.SpectatorId && !p.IsDeleted);
                if (profile != null)
                {
                    profile.Balance = (profile.Balance ?? 0) + (long)bet.BetAmount;
                    profile.UpdatedAt = DateTimeOffset.UtcNow;
                    await uow.GetRepository<UserProfile>().UpdateAsync(profile);
                }
            }

            await uow.SaveAsync();
        }
    }
}
