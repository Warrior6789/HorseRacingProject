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
public class RaceSettlementServiceIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RaceSettlementServiceIntegrationTests(PostgresContainerFixture fixture)
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

    private static IServiceScopeFactory CreateScopeFactory(IUnitofWork uow)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IUnitofWork))).Returns(uow);

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

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.All).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<RaceHub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        return hubContext.Object;
    }

    private class Fixture
    {
        public required HorseRacingDataContext Db;
        public required Race Race;
        public required Horse[] Horses;
        public required Registration[] Registrations;
        public required JockeyProfile[] JockeyProfiles;
        public required UserProfile[] OwnerProfiles;
    }

    private static async Task<Fixture> SeedThreeFinisherRaceAsync(HorseRacingDataContext db, bool withBet = true)
    {
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Racecourse = racecourse,
            Status = RaceStatus.Finished,
            PrizePool = 1_000_000m,
            StartTime = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var posConfig = new PositionPrizeConfig
        {
            PositionPrizeConfigId = Guid.NewGuid(),
            Pos1Ratio = 0.5f,
            Pos2Ratio = 0.3f,
            Pos3Ratio = 0.2f,
            Status = ConfigStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var jockeyConfig = new JockeyRewardConfig
        {
            JockeyRewardConfigId = Guid.NewGuid(),
            WinCut = 0.10f,
            PlaceCut = 0.05f,
            Status = ConfigStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var owners = new Account[3];
        var jockeys = new Account[3];
        var horses = new Horse[3];
        var registrations = new Registration[3];
        var jockeyProfiles = new JockeyProfile[3];
        var ownerProfiles = new UserProfile[3];

        var spectator = new Account { Id = Guid.NewGuid(), Email = "spectator@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        var spectatorProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = spectator.Id, Balance = 0 };

        for (int i = 0; i < 3; i++)
        {
            owners[i] = new Account { Id = Guid.NewGuid(), Email = $"owner{i}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
            jockeys[i] = new Account { Id = Guid.NewGuid(), Email = $"jockey{i}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
            horses[i] = new Horse { Id = Guid.NewGuid(), OwnerId = owners[i].Id, HorseName = $"Horse{i}", Status = HorseStatus.Healthy, RecordWins = 0 };
            registrations[i] = new Registration
            {
                RegistrationId = Guid.NewGuid(),
                RaceId = race.RaceId,
                HorseId = horses[i].Id,
                JockeyId = jockeys[i].Id,
                Status = RegistrationStatus.Confirmed
            };
            jockeyProfiles[i] = new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = jockeys[i].Id, Balance = 0 };
            ownerProfiles[i] = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owners[i].Id, Balance = 0 };
        }

        db.AddRange(owners);
        db.AddRange(jockeys);
        db.Add(spectator);
        db.AddRange(horses);
        db.Add(racecourse);
        db.Add(race);
        db.Add(posConfig);
        db.Add(jockeyConfig);
        db.AddRange(registrations);
        db.AddRange(jockeyProfiles);
        db.AddRange(ownerProfiles);
        db.Add(spectatorProfile);

        for (int i = 0; i < 3; i++)
        {
            db.Add(new RaceResult
            {
                ResultId = Guid.NewGuid(),
                RegistrationId = registrations[i].RegistrationId,
                FinishPosition = i + 1,
                IsDisqualified = false
            });
        }

        if (withBet)
        {
            db.Add(new Bet
            {
                BetId = Guid.NewGuid(),
                SpectatorId = spectator.Id,
                RegistrationId = registrations[0].RegistrationId,
                BetAmount = 10_000m,
                BetType = BetType.Win,
                Status = BetStatus.Active
            });
        }

        await db.SaveChangesAsync();

        return new Fixture
        {
            Db = db,
            Race = race,
            Horses = horses,
            Registrations = registrations,
            JockeyProfiles = jockeyProfiles,
            OwnerProfiles = ownerProfiles
        };
    }

    [Fact]
    public async Task TrySettleAsync_OnlyPositions1And2_GetJockeyCut()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        Fixture fixture = await SeedThreeFinisherRaceAsync(db);

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());

        await service.TrySettleAsync(fixture.Race.RaceId);

        List<Prize> prizes = await db.Prizes.ToListAsync();

        decimal JockeyAmountFor(Guid registrationId) =>
            prizes.Single(p => p.RegistrationId == registrationId && p.PrizeType == PrizeType.Jockey).Amount ?? 0m;

        decimal OwnerAmountFor(Guid registrationId) =>
            prizes.Single(p => p.RegistrationId == registrationId && p.PrizeType == PrizeType.Owner).Amount ?? 0m;

        Guid reg1 = fixture.Registrations[0].RegistrationId;
        Guid reg2 = fixture.Registrations[1].RegistrationId;
        Guid reg3 = fixture.Registrations[2].RegistrationId;

        Assert.True(JockeyAmountFor(reg1) > 0);
        Assert.True(JockeyAmountFor(reg2) > 0);
        Assert.Equal(0m, JockeyAmountFor(reg3));

        Assert.Equal(200_000m, OwnerAmountFor(reg3));
    }

    [Fact]
    public async Task TrySettleAsync_WinningHorse_IncrementsRecordWins()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        Fixture fixture = await SeedThreeFinisherRaceAsync(db);

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());

        await service.TrySettleAsync(fixture.Race.RaceId);

        Horse winner = await db.Horses.AsNoTracking().SingleAsync(h => h.Id == fixture.Horses[0].Id);
        Horse runnerUp = await db.Horses.AsNoTracking().SingleAsync(h => h.Id == fixture.Horses[1].Id);

        Assert.Equal(1, winner.RecordWins);
        Assert.Equal(0, runnerUp.RecordWins);
    }

    [Fact]
    public async Task TrySettleAsync_RaceWithNoBets_StillDistributesPositionPrizes()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        Fixture fixture = await SeedThreeFinisherRaceAsync(db, withBet: false);

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());

        await service.TrySettleAsync(fixture.Race.RaceId);

        List<Prize> prizes = await db.Prizes.ToListAsync();
        Assert.NotEmpty(prizes);

        decimal OwnerAmountFor(Guid registrationId) =>
            prizes.Single(p => p.RegistrationId == registrationId && p.PrizeType == PrizeType.Owner).Amount ?? 0m;

        Guid reg1 = fixture.Registrations[0].RegistrationId;
        Assert.True(OwnerAmountFor(reg1) > 0);
    }

    [Fact]
    public async Task TrySettleAsync_DisqualifiedFirstPlace_ExcludedFromPrizesAndWinIncrement()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        Fixture fixture = await SeedThreeFinisherRaceAsync(db, withBet: false);

        RaceResult firstPlaceResult = await db.RaceResults.SingleAsync(r => r.RegistrationId == fixture.Registrations[0].RegistrationId);
        firstPlaceResult.IsDisqualified = true;
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());
        await service.TrySettleAsync(fixture.Race.RaceId);

        Guid disqualifiedReg = fixture.Registrations[0].RegistrationId;
        Guid newFirstReg = fixture.Registrations[1].RegistrationId;

        Assert.False(await db.Prizes.AsNoTracking().AnyAsync(p => p.RegistrationId == disqualifiedReg));
        Assert.True(await db.Prizes.AsNoTracking().AnyAsync(p => p.RegistrationId == newFirstReg && p.PrizeType == PrizeType.Owner && p.Amount == 562_500m));

        Horse disqualifiedHorse = await db.Horses.AsNoTracking().SingleAsync(h => h.Id == fixture.Horses[0].Id);
        Horse promotedHorse = await db.Horses.AsNoTracking().SingleAsync(h => h.Id == fixture.Horses[1].Id);
        Assert.Equal(0, disqualifiedHorse.RecordWins);
        Assert.Equal(1, promotedHorse.RecordWins);
    }

    [Fact]
    public async Task TrySettleAsync_OnlyTwoRegisteredFinishers_DistributesEntirePurseAcrossThemOnly()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        Fixture fixture = await SeedThreeFinisherRaceAsync(db, withBet: false);

        await db.RaceResults
            .Where(r => r.RegistrationId == fixture.Registrations[2].RegistrationId)
            .ExecuteDeleteAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());
        await service.TrySettleAsync(fixture.Race.RaceId);

        List<Prize> prizes = await db.Prizes.AsNoTracking().ToListAsync();
        Assert.DoesNotContain(prizes, p => p.RegistrationId == fixture.Registrations[2].RegistrationId);

        decimal totalDistributed = prizes.Sum(p => p.Amount ?? 0m);
        Assert.Equal(fixture.Race.PrizePool, totalDistributed);
    }
}
