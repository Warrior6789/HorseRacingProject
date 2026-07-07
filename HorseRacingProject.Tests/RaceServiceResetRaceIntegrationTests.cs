using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HorseRacingProject.Tests;

[Collection("Postgres")]
public class RaceServiceResetRaceIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RaceServiceResetRaceIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<HorseRacingDataContext> CreateContextAsync()
    {
        await _fixture.ResetAsync();
        DbContextOptions<HorseRacingDataContext> options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static IHubContext<RaceHub> CreateHubContext()
    {
        var clientProxy = new Mock<IClientProxy>();
        clientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<RaceHub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        return hubContext.Object;
    }

    private static RaceEngineService CreateEngine() =>
        new(CreateHubContext(), Mock.Of<IServiceScopeFactory>(), Mock.Of<IRaceSettlementService>());

    private class Fixture : IDisposable
    {
        public required HorseRacingDataContext Db;
        public required RaceService Service;
        public required Race Race;
        public required Registration Registration;
        public required Account Owner;
        public required Account Jockey;
        public required Account Spectator;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedFinishedRaceAsync()
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var referee = new Account { Id = Guid.NewGuid(), Email = "ref@test.com", PasswordHash = "x", Role = AccountRole.Referee, Status = AccountStatus.Active };
        var owner = new Account { Id = Guid.NewGuid(), Email = "owner@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey = new Account { Id = Guid.NewGuid(), Email = "jockey@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var spectator = new Account { Id = Guid.NewGuid(), Email = "spectator@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };

        var ownerProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owner.Id, Balance = 5_000 };
        var spectatorProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = spectator.Id, Balance = 2_000 };
        var jockeyProfile = new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = jockey.Id, Balance = 800 };

        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = RaceStatus.Finished,
            RegistrationFee = 1_000m,
            PrizePool = 0m,
            StartTime = DateTimeOffset.UtcNow.AddDays(-1),
            EndTime = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(30)
        };
        var registration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = race.RaceId,
            HorseId = horse.Id,
            JockeyId = jockey.Id,
            Status = RegistrationStatus.Confirmed
        };

        var bet = new Bet
        {
            BetId = Guid.NewGuid(),
            SpectatorId = spectator.Id,
            RegistrationId = registration.RegistrationId,
            BetAmount = 500m,
            BetType = BetType.Win,
            Status = BetStatus.Won,
            PayoutRatio = 2.0f
        };

        var raceResult = new RaceResult
        {
            ResultId = Guid.NewGuid(),
            RegistrationId = registration.RegistrationId,
            FinishPosition = 1,
            IsDisqualified = false
        };

        var refereeReport = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = race.RaceId,
            RefereeId = referee.Id,
            RegistrationId = registration.RegistrationId,
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Approved,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var ownerPrize = new Prize
        {
            PrizeId = Guid.NewGuid(),
            RegistrationId = registration.RegistrationId,
            PrizeType = PrizeType.Owner,
            Amount = 3_000m,
            DistributedAt = DateTimeOffset.UtcNow
        };
        var jockeyPrize = new Prize
        {
            PrizeId = Guid.NewGuid(),
            RegistrationId = registration.RegistrationId,
            PrizeType = PrizeType.Jockey,
            Amount = 800m,
            DistributedAt = DateTimeOffset.UtcNow
        };

        var racePool = new RacePool
        {
            RacePoolId = Guid.NewGuid(),
            RaceId = race.RaceId,
            BetType = BetType.Win,
            TotalAmount = 500m
        };

        db.AddRange(racecourse, referee, owner, jockey, spectator, horse,
            ownerProfile, spectatorProfile, jockeyProfile,
            race, registration, bet, raceResult, refereeReport, ownerPrize, jockeyPrize, racePool);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceService(uow, CreateEngine(), Mock.Of<ICloudinaryService>(), CreateHubContext());

        return new Fixture { Db = db, Service = service, Race = race, Registration = registration, Owner = owner, Jockey = jockey, Spectator = spectator };
    }

    [Fact]
    public async Task ResetRaceAsync_ReversesBalancesAndDeletesRaceData()
    {
        using Fixture fixture = await SeedFinishedRaceAsync();

        await fixture.Service.ResetRaceAsync(fixture.Race.RaceId);

        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Equal(RaceStatus.Scheduled, race.Status);
        Assert.Null(race.EndTime);
        Assert.Equal(0m, race.PrizePool);

        bool registrationExists = await fixture.Db.Registrations.AsNoTracking()
            .AnyAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.False(registrationExists);

        Assert.False(await fixture.Db.Bets.AsNoTracking().AnyAsync(b => b.RegistrationId == fixture.Registration.RegistrationId));
        Assert.False(await fixture.Db.RaceResults.AsNoTracking().AnyAsync(r => r.RegistrationId == fixture.Registration.RegistrationId));
        Assert.False(await fixture.Db.RefereeReports.AsNoTracking().AnyAsync(r => r.RaceId == fixture.Race.RaceId));
        Assert.False(await fixture.Db.Prizes.AsNoTracking().AnyAsync(p => p.RegistrationId == fixture.Registration.RegistrationId));
        Assert.False(await fixture.Db.RacePools.AsNoTracking().AnyAsync(p => p.RaceId == fixture.Race.RaceId));

        UserProfile spectatorProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Spectator.Id);
        Assert.Equal(1_500L, spectatorProfile.Balance);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(3_000L, ownerProfile.Balance);

        JockeyProfile jockeyProfile = await fixture.Db.JockeyProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Jockey.Id);
        Assert.Equal(0L, jockeyProfile.Balance);
    }
}
