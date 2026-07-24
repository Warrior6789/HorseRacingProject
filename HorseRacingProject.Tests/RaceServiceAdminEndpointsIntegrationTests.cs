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
public class RaceServiceAdminEndpointsIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RaceServiceAdminEndpointsIntegrationTests(PostgresContainerFixture fixture)
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
        public required Race Race;
        public required Registration[] Registrations;
        public required Account[] Spectators;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedAsync(RaceStatus raceStatus)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = raceStatus,
            StartTime = DateTimeOffset.UtcNow.AddHours(1),
            PrizePool = 1_000_000m
        };

        var owners = new Account[3];
        var jockeys = new Account[3];
        var horses = new Horse[3];
        var registrations = new Registration[3];

        for (int i = 0; i < 3; i++)
        {
            owners[i] = new Account { Id = Guid.NewGuid(), Email = $"owner{i}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
            jockeys[i] = new Account { Id = Guid.NewGuid(), Email = $"jockey{i}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
            horses[i] = new Horse { Id = Guid.NewGuid(), OwnerId = owners[i].Id, HorseName = $"Horse{i}", Status = HorseStatus.Healthy };
            registrations[i] = new Registration
            {
                RegistrationId = Guid.NewGuid(),
                RaceId = race.RaceId,
                HorseId = horses[i].Id,
                JockeyId = jockeys[i].Id,
                Status = RegistrationStatus.Confirmed
            };
            db.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owners[i].Id, Balance = 0 });
            db.Add(new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = jockeys[i].Id, Balance = 0 });
        }

        var spectators = new Account[3];
        for (int i = 0; i < 3; i++)
        {
            spectators[i] = new Account { Id = Guid.NewGuid(), Email = $"spectator{i}@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        }
        db.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = spectators[0].Id, Balance = 10_000 });
        db.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = spectators[1].Id, Balance = 10_000 });
        db.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = spectators[2].Id, Balance = 100 });

        db.AddRange(racecourse, race);
        db.AddRange(owners);
        db.AddRange(jockeys);
        db.AddRange(horses);
        db.AddRange(registrations);
        db.AddRange(spectators);

        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceService(uow, engine: null!, cloudinaryService: Mock.Of<ICloudinaryService>(), hubContext: CreateHubContext());

        return new Fixture { Db = db, Service = service, Race = race, Registrations = registrations, Spectators = spectators };
    }

    [Fact]
    public async Task GetRacePoolOverviewAsync_ReturnsBetsAndPoolSummaries()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.BettingClosed);

        fixture.Db.Add(new RacePool { RacePoolId = Guid.NewGuid(), RaceId = fixture.Race.RaceId, BetType = BetType.Win, TotalAmount = 30_000m });
        fixture.Db.Add(new Bet
        {
            BetId = Guid.NewGuid(),
            SpectatorId = fixture.Spectators[0].Id,
            RegistrationId = fixture.Registrations[0].RegistrationId,
            BetAmount = 10_000m,
            BetType = BetType.Win,
            Status = BetStatus.Active
        });
        fixture.Db.Add(new Bet
        {
            BetId = Guid.NewGuid(),
            SpectatorId = fixture.Spectators[1].Id,
            RegistrationId = fixture.Registrations[1].RegistrationId,
            BetAmount = 20_000m,
            BetType = BetType.Win,
            Status = BetStatus.Active
        });
        await fixture.Db.SaveChangesAsync();

        RacePoolOverviewResponse overview = await fixture.Service.GetRacePoolOverviewAsync(fixture.Race.RaceId);

        Assert.Equal(30_000m, overview.TotalPoolAmount);
        Assert.Equal(2, overview.Bets.Count);
        Assert.Single(overview.Pools);
        Assert.Equal(2, overview.Pools[0].BetCount);
    }

    [Fact]
    public async Task GetRacePoolOverviewAsync_RaceNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.BettingClosed);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.GetRacePoolOverviewAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPrizePreviewAsync_NotYetSettled_ReturnsProjectedNotFinal()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.BettingClosed);

        fixture.Db.Add(new PositionPrizeConfig
        {
            PositionPrizeConfigId = Guid.NewGuid(),
            Pos1Ratio = 0.5f,
            Pos2Ratio = 0.3f,
            Pos3Ratio = 0.2f,
            Status = ConfigStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        });
        fixture.Db.Add(new JockeyRewardConfig
        {
            JockeyRewardConfigId = Guid.NewGuid(),
            WinCut = 0.10f,
            PlaceCut = 0.05f,
            Status = ConfigStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        });
        for (int i = 0; i < 3; i++)
        {
            fixture.Db.Add(new RaceResult
            {
                ResultId = Guid.NewGuid(),
                RegistrationId = fixture.Registrations[i].RegistrationId,
                FinishPosition = i + 1,
                IsDisqualified = false
            });
        }
        await fixture.Db.SaveChangesAsync();

        RacePrizePreviewResponse preview = await fixture.Service.GetPrizePreviewAsync(fixture.Race.RaceId);

        Assert.False(preview.IsFinal);
        Assert.Equal(1_000_000m, preview.RacePurse);
        Assert.Equal(3, preview.Items.Count);
        Assert.Equal(500_000m, preview.Items.Single(i => i.Position == 1).PositionPrize);
    }

    [Fact]
    public async Task GetPrizePreviewAsync_AlreadySettled_ReturnsFinalFromActualPrizes()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished);

        fixture.Db.Add(new Prize { PrizeId = Guid.NewGuid(), RegistrationId = fixture.Registrations[0].RegistrationId, PrizeType = PrizeType.Owner, Amount = 450_000m, DistributedAt = DateTimeOffset.UtcNow });
        fixture.Db.Add(new Prize { PrizeId = Guid.NewGuid(), RegistrationId = fixture.Registrations[0].RegistrationId, PrizeType = PrizeType.Jockey, Amount = 50_000m, DistributedAt = DateTimeOffset.UtcNow });
        fixture.Db.Add(new RaceResult { ResultId = Guid.NewGuid(), RegistrationId = fixture.Registrations[0].RegistrationId, FinishPosition = 1, IsDisqualified = false });
        await fixture.Db.SaveChangesAsync();

        RacePrizePreviewResponse preview = await fixture.Service.GetPrizePreviewAsync(fixture.Race.RaceId);

        Assert.True(preview.IsFinal);
        Assert.Equal(500_000m, preview.RacePurse);
        RacePrizePreviewItemResponse item = Assert.Single(preview.Items);
        Assert.Equal(450_000m, item.OwnerAmount);
        Assert.Equal(50_000m, item.JockeyAmount);
    }

    [Fact]
    public async Task GetPrizePreviewAsync_RaceNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.BettingClosed);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.GetPrizePreviewAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetTakeoutLedgerPagedAsync_FiltersByRaceAndBetTypeAndPaginates()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished);

        var otherRace = new Race { RaceId = Guid.NewGuid(), RacecourseId = fixture.Race.RacecourseId, Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow };
        fixture.Db.Add(otherRace);
        fixture.Db.Add(new TakeoutLedger { TakeoutLedgerId = Guid.NewGuid(), RaceId = fixture.Race.RaceId, BetType = BetType.Win, TotalPool = 100_000m, TakeoutPercentage = 0.1f, TakeoutAmount = 10_000m, CreatedAt = DateTimeOffset.UtcNow });
        fixture.Db.Add(new TakeoutLedger { TakeoutLedgerId = Guid.NewGuid(), RaceId = fixture.Race.RaceId, BetType = BetType.Place, TotalPool = 50_000m, TakeoutPercentage = 0.1f, TakeoutAmount = 5_000m, CreatedAt = DateTimeOffset.UtcNow });
        fixture.Db.Add(new TakeoutLedger { TakeoutLedgerId = Guid.NewGuid(), RaceId = otherRace.RaceId, BetType = BetType.Win, TotalPool = 200_000m, TakeoutPercentage = 0.1f, TakeoutAmount = 20_000m, CreatedAt = DateTimeOffset.UtcNow });
        await fixture.Db.SaveChangesAsync();

        TakeoutLedgerPagedResponse allForRace = await fixture.Service.GetTakeoutLedgerPagedAsync(page: 1, pageSize: 10, raceId: fixture.Race.RaceId);
        Assert.Equal(2, allForRace.TotalCount);
        Assert.Equal(15_000m, allForRace.TotalTakeoutAmount);

        TakeoutLedgerPagedResponse winOnly = await fixture.Service.GetTakeoutLedgerPagedAsync(page: 1, pageSize: 10, raceId: fixture.Race.RaceId, betType: "Win");
        Assert.Equal(1, winOnly.TotalCount);
        Assert.Equal(10_000m, winOnly.TotalTakeoutAmount);

        TakeoutLedgerPagedResponse allRaces = await fixture.Service.GetTakeoutLedgerPagedAsync(page: 1, pageSize: 10);
        Assert.Equal(3, allRaces.TotalCount);
    }

    [Fact]
    public async Task GetTakeoutLedgerPagedAsync_InvalidBetType_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.GetTakeoutLedgerPagedAsync(page: 1, pageSize: 10, betType: "Exacta"));
    }
}
