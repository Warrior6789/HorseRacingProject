using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HorseRacingProject.Tests;

[Collection("Postgres")]
public class RaceServiceAdvanceStatusIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RaceServiceAdvanceStatusIntegrationTests(PostgresContainerFixture fixture)
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

    private class Fixture : IDisposable
    {
        public required HorseRacingDataContext Db;
        public required RaceService Service;
        public required Racecourse Racecourse;
        public required Race Race;
        public required Account Referee;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedAsync(RaceStatus raceStatus, bool withActiveConfigs = true, Guid? refereeId = null)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var referee = new Account { Id = Guid.NewGuid(), Email = "ref@test.com", PasswordHash = "x", Role = AccountRole.Referee, Status = AccountStatus.Active };

        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = raceStatus,
            StartTime = DateTimeOffset.UtcNow.AddHours(1),
            EndTime = DateTimeOffset.UtcNow.AddHours(1).AddMinutes(30),
            RefereeId = refereeId
        };

        db.AddRange(racecourse, referee, race);

        if (withActiveConfigs)
        {
            db.Add(new PositionPrizeConfig
            {
                PositionPrizeConfigId = Guid.NewGuid(),
                Pos1Ratio = 0.5f,
                Pos2Ratio = 0.3f,
                Pos3Ratio = 0.2f,
                Status = ConfigStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.Add(new JockeyRewardConfig
            {
                JockeyRewardConfigId = Guid.NewGuid(),
                WinCut = 0.10f,
                PlaceCut = 0.05f,
                Status = ConfigStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.Add(new TakeoutConfig
            {
                TakeoutConfigId = Guid.NewGuid(),
                TakeoutPercentage = 0.20f,
                Status = ConfigStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceService(uow, engine: null!, cloudinaryService: Mock.Of<ICloudinaryService>(), hubContext: CreateHubContext());

        return new Fixture { Db = db, Service = service, Racecourse = racecourse, Race = race, Referee = referee };
    }

    private async Task AddConfirmedRegistrationsAsync(HorseRacingDataContext db, Guid raceId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var owner = new Account { Id = Guid.NewGuid(), Email = $"owner{i}-{raceId:N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
            var jockey = new Account { Id = Guid.NewGuid(), Email = $"jockey{i}-{raceId:N}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
            var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = $"Horse{i}", Status = HorseStatus.Healthy };
            var registration = new Registration
            {
                RegistrationId = Guid.NewGuid(),
                RaceId = raceId,
                HorseId = horse.Id,
                JockeyId = jockey.Id,
                Status = RegistrationStatus.Confirmed
            };
            db.AddRange(owner, jockey, horse, registration);
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_Scheduled_WithActiveConfigs_MovesToBettingOpen()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, withActiveConfigs: true);

        RaceResponse response = await fixture.Service.AdvanceRaceStatusAsync(fixture.Race.RaceId);

        Assert.Equal(RaceStatus.BettingOpen.ToString(), response.Status);
        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Equal(RaceStatus.BettingOpen, race.Status);
        Assert.NotNull(race.PositionPrizeConfigId);
        Assert.NotNull(race.JockeyRewardConfigId);
        Assert.NotNull(race.TakeoutConfigId);
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_Scheduled_NoActiveConfigs_ThrowsAndLeavesStatusUnchanged()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, withActiveConfigs: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AdvanceRaceStatusAsync(fixture.Race.RaceId));

        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Equal(RaceStatus.Scheduled, race.Status);
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_BettingClosedWithFewerThan3Confirmed_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.BettingClosed);
        await AddConfirmedRegistrationsAsync(fixture.Db, fixture.Race.RaceId, count: 2);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AdvanceRaceStatusAsync(fixture.Race.RaceId));
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_BettingClosedWithoutReferee_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.BettingClosed, refereeId: null);
        await AddConfirmedRegistrationsAsync(fixture.Db, fixture.Race.RaceId, count: 3);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AdvanceRaceStatusAsync(fixture.Race.RaceId));
        Assert.Contains("referee", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_AnotherRaceLiveAtSameRacecourse_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.BettingClosed);
        await AddConfirmedRegistrationsAsync(fixture.Db, fixture.Race.RaceId, count: 3);
        fixture.Race.RefereeId = fixture.Referee.Id;
        await fixture.Db.SaveChangesAsync();

        var otherLiveRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = fixture.Racecourse.Id,
            Status = RaceStatus.Live,
            StartTime = DateTimeOffset.UtcNow
        };
        fixture.Db.Add(otherLiveRace);
        await fixture.Db.SaveChangesAsync();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AdvanceRaceStatusAsync(fixture.Race.RaceId));
        Assert.Contains("Live", ex.Message);
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_RefereeAlreadyLiveInOtherRace_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.BettingClosed);
        await AddConfirmedRegistrationsAsync(fixture.Db, fixture.Race.RaceId, count: 3);
        fixture.Race.RefereeId = fixture.Referee.Id;
        await fixture.Db.SaveChangesAsync();

        var otherRacecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Other Track" };
        var otherLiveRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = otherRacecourse.Id,
            Status = RaceStatus.Live,
            RaceNumber = 7,
            RefereeId = fixture.Referee.Id,
            StartTime = DateTimeOffset.UtcNow
        };
        fixture.Db.AddRange(otherRacecourse, otherLiveRace);
        await fixture.Db.SaveChangesAsync();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AdvanceRaceStatusAsync(fixture.Race.RaceId));
        Assert.Contains("Race #7", ex.Message);
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_BettingClosedWithAllPrerequisitesMet_MovesToLive()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.BettingClosed);
        await AddConfirmedRegistrationsAsync(fixture.Db, fixture.Race.RaceId, count: 3);
        fixture.Race.RefereeId = fixture.Referee.Id;
        await fixture.Db.SaveChangesAsync();

        RaceResponse response = await fixture.Service.AdvanceRaceStatusAsync(fixture.Race.RaceId);

        Assert.Equal(RaceStatus.Live.ToString(), response.Status);
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_FinishedRace_CannotAdvance_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AdvanceRaceStatusAsync(fixture.Race.RaceId));
        Assert.Contains("cannot be advanced", ex.Message);
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_RaceNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.Service.AdvanceRaceStatusAsync(Guid.NewGuid()));
    }
}
