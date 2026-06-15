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

                // Scheduled → BettingOpen (trước 30 phút)
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

                // BettingOpen → BettingClosed (trước 5 phút)
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

                // BettingClosed → Live (race trước cùng racecourse đã Finished, đủ người)
                // Group theo racecourse, mỗi racecourse chỉ cho race StartTime sớm nhất lên Live
                List<Race> toStart = await uow.GetRepository<Race>().Entities
                    .Where(r => r.Status == "BettingClosed"
                                && r.StartTime <= now
                                && !r.IsDeleted)
                    .OrderBy(r => r.StartTime)
                    .ToListAsync();

                HashSet<Guid> startedRacecourses = new();

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
                            _angles[registration.HorseId] = Math.PI;

                        double noise = (_random.NextDouble() - 0.5) * 0.06;
                        double speed = 0.19 + noise;
                        _angles[registration.HorseId] += speed * 0.1;

                        double currentAngle = _angles[registration.HorseId];
                        int lap = (int)((currentAngle - Math.PI) / (2 * Math.PI));
                        bool isFinished = currentAngle >= Math.PI + 2 * 2 * Math.PI;

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

                        await _hubContext.Clients.Group($"race-{race.RaceId}")
                            .SendAsync("RaceUpdate", new
                            {
                                raceId = race.RaceId,
                                tick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                status = "Finished",
                                horses = horseStates
                            });

                        foreach (var reg in registrations)
                            _angles.Remove(reg.HorseId);
                    }
                }

                await Task.Delay(100, stoppingToken);
            }
        }
    }
}
