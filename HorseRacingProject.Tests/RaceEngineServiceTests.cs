using System.Reflection;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HorseRacingProject.Tests;

public class RaceEngineServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static IServiceScopeFactory CreateScopeFactory(IUnitofWork uow)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IUnitofWork))).Returns(uow);
        provider.Setup(p => p.GetService(typeof(IRegistrationService))).Returns(new RegistrationService(uow, CreateHubContext()));

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private static IHubContext<RaceHub> CreateHubContext()
    {
        var clientProxy = new Mock<IClientProxy>();
        clientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        var groups = new Mock<IGroupManager>();

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<RaceHub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        return hubContext.Object;
    }

    private static async Task RunOneIterationAsync(RaceEngineService engine, int cancelAfterMs = 250)
    {
        MethodInfo method = typeof(RaceEngineService)
            .GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(cancelAfterMs);

        var task = (Task)method.Invoke(engine, new object[] { cts.Token })!;
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static RaceEngineService CreateEngine(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new RaceEngineService(CreateHubContext(), CreateScopeFactory(uow), Mock.Of<IRaceSettlementService>(), NullLogger<RaceEngineService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_RestingHorsePastSevenDays_BecomesHealthy()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Resting };
        var finishedRace = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow.AddDays(-10), EndTime = DateTimeOffset.UtcNow.AddDays(-8) };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = finishedRace.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };

        db.AddRange(racecourse, owner, jockey, horse, finishedRace, registration);
        await db.SaveChangesAsync();

        RaceEngineService engine = CreateEngine(db);

        await RunOneIterationAsync(engine);

        Horse updated = await db.Horses.AsNoTracking().SingleAsync(h => h.Id == horse.Id);
        Assert.Equal(HorseStatus.Healthy, updated.Status);
    }

    [Fact]
    public async Task ExecuteAsync_RestingHorseWithinSevenDays_StaysResting()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Resting };
        var finishedRace = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow.AddDays(-2), EndTime = DateTimeOffset.UtcNow.AddDays(-2) };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = finishedRace.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };

        db.AddRange(racecourse, owner, jockey, horse, finishedRace, registration);
        await db.SaveChangesAsync();

        RaceEngineService engine = CreateEngine(db);

        await RunOneIterationAsync(engine);

        Horse updated = await db.Horses.AsNoTracking().SingleAsync(h => h.Id == horse.Id);
        Assert.Equal(HorseStatus.Resting, updated.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ScheduledRaceWithinOpenWindow_BecomesBettingOpen()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddMinutes(20) };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();

        RaceEngineService engine = CreateEngine(db);

        await RunOneIterationAsync(engine);

        Race updated = await db.Races.AsNoTracking().SingleAsync(r => r.RaceId == race.RaceId);
        Assert.Equal(RaceStatus.BettingOpen, updated.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ScheduledRaceOutsideOpenWindow_StaysScheduled()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();

        RaceEngineService engine = CreateEngine(db);

        await RunOneIterationAsync(engine);

        Race updated = await db.Races.AsNoTracking().SingleAsync(r => r.RaceId == race.RaceId);
        Assert.Equal(RaceStatus.Scheduled, updated.Status);
    }

    [Fact]
    public async Task ExecuteAsync_BettingOpenRaceWithinCloseWindow_BecomesBettingClosed()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.BettingOpen, StartTime = DateTimeOffset.UtcNow.AddMinutes(3) };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();

        RaceEngineService engine = CreateEngine(db);

        await RunOneIterationAsync(engine);

        Race updated = await db.Races.AsNoTracking().SingleAsync(r => r.RaceId == race.RaceId);
        Assert.Equal(RaceStatus.BettingClosed, updated.Status);
    }

    [Fact]
    public async Task ExecuteAsync_RaceGoesLiveWithPendingRegistration_AutoRejectsAndRefundsFee()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.BettingClosed, StartTime = DateTimeOffset.UtcNow.AddMinutes(-1), RegistrationFee = 1000m };
        db.AddRange(racecourse, race);

        for (int i = 0; i < 3; i++)
        {
            var owner  = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
            var jockey = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
            var horse  = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = $"Confirmed{i}", Status = HorseStatus.Healthy };
            db.AddRange(owner, jockey, horse);
            db.Registrations.Add(new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed });
        }

        var abandonedOwner   = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var abandonedJockey  = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var abandonedHorse   = new Horse { Id = Guid.NewGuid(), OwnerId = abandonedOwner.Id, HorseName = "Abandoned", Status = HorseStatus.Healthy };
        var abandonedProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = abandonedOwner.Id, FullName = "Owner", Phone = "0900000000", Balance = 5000 };
        db.AddRange(abandonedOwner, abandonedJockey, abandonedHorse, abandonedProfile);
        var pendingReg = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = abandonedHorse.Id, JockeyId = abandonedJockey.Id, Status = RegistrationStatus.Pending };
        db.Registrations.Add(pendingReg);

        await db.SaveChangesAsync();

        RaceEngineService engine = CreateEngine(db);

        await RunOneIterationAsync(engine);

        Race updatedRace = await db.Races.AsNoTracking().SingleAsync(r => r.RaceId == race.RaceId);
        Assert.Equal(RaceStatus.Live, updatedRace.Status);

        Registration updatedReg = await db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == pendingReg.RegistrationId);
        Assert.Equal(RegistrationStatus.Rejected, updatedReg.Status);

        UserProfile updatedProfile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == abandonedOwner.Id);
        Assert.Equal(6000, updatedProfile.Balance);
    }
}
