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
public class BetSettlementIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public BetSettlementIntegrationTests(PostgresContainerFixture fixture)
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

    private class RaceFixture
    {
        public required Race Race;
        public required Registration[] Registrations;
        public required Account[] Spectators;
    }

    private static async Task<RaceFixture> SeedFinishedRaceAsync(HorseRacingDataContext db, float takeoutPercentage, int spectatorCount, Racecourse? racecourse = null)
    {
        bool isNewRacecourse = racecourse == null;
        racecourse ??= new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Racecourse = racecourse,
            Status = RaceStatus.Finished,
            PrizePool = 1_000_000m,
            StartTime = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var owners = new Account[3];
        var jockeys = new Account[3];
        var horses = new Horse[3];
        var registrations = new Registration[3];

        for (int i = 0; i < 3; i++)
        {
            owners[i] = new Account { Id = Guid.NewGuid(), Email = $"owner{i}-{race.RaceId:N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
            jockeys[i] = new Account { Id = Guid.NewGuid(), Email = $"jockey{i}-{race.RaceId:N}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
            horses[i] = new Horse { Id = Guid.NewGuid(), OwnerId = owners[i].Id, HorseName = $"Horse{i}", Status = HorseStatus.Healthy, RecordWins = 0 };
            registrations[i] = new Registration
            {
                RegistrationId = Guid.NewGuid(),
                RaceId = race.RaceId,
                HorseId = horses[i].Id,
                JockeyId = jockeys[i].Id,
                Status = RegistrationStatus.Confirmed
            };
            db.Add(new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = jockeys[i].Id, Balance = 0 });
            db.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owners[i].Id, Balance = 0 });
        }

        var spectators = new Account[spectatorCount];
        for (int i = 0; i < spectatorCount; i++)
        {
            spectators[i] = new Account { Id = Guid.NewGuid(), Email = $"spectator{i}-{race.RaceId:N}@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
            db.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = spectators[i].Id, Balance = 0 });
        }

        db.AddRange(owners);
        db.AddRange(jockeys);
        db.AddRange(spectators);
        db.AddRange(horses);
        db.Add(race);
        db.AddRange(registrations);

        TakeoutConfig? takeoutConfig;
        if (isNewRacecourse)
        {
            db.Add(racecourse);
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
            takeoutConfig = new TakeoutConfig
            {
                TakeoutConfigId = Guid.NewGuid(),
                TakeoutPercentage = takeoutPercentage,
                Status = ConfigStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(takeoutConfig);
        }
        else
        {
            takeoutConfig = await db.TakeoutConfigs.FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active);
        }
        race.TakeoutConfigId = takeoutConfig?.TakeoutConfigId;

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

        await db.SaveChangesAsync();

        return new RaceFixture { Race = race, Registrations = registrations, Spectators = spectators };
    }

    private static void AddBet(HorseRacingDataContext db, Account spectator, Registration registration, decimal amount, BetType betType)
    {
        db.Add(new Bet
        {
            BetId = Guid.NewGuid(),
            SpectatorId = spectator.Id,
            RegistrationId = registration.RegistrationId,
            BetAmount = amount,
            BetType = betType,
            Status = BetStatus.Active
        });
    }

    [Fact]
    public async Task TrySettleAsync_WinningBet_PaysNetPoolShareAndRecordsTakeoutLedger()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        RaceFixture fixture = await SeedFinishedRaceAsync(db, takeoutPercentage: 0.10f, spectatorCount: 2);

        AddBet(db, fixture.Spectators[0], fixture.Registrations[0], 100_000m, BetType.Win);
        AddBet(db, fixture.Spectators[1], fixture.Registrations[1], 50_000m, BetType.Win);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());
        await service.TrySettleAsync(fixture.Race.RaceId);

        Bet winningBet = await db.Bets.AsNoTracking().SingleAsync(b => b.SpectatorId == fixture.Spectators[0].Id);
        Bet losingBet = await db.Bets.AsNoTracking().SingleAsync(b => b.SpectatorId == fixture.Spectators[1].Id);

        Assert.Equal(BetStatus.Won, winningBet.Status);
        Assert.Equal(1.35f, winningBet.PayoutRatio);
        Assert.Equal(BetStatus.Lost, losingBet.Status);
        Assert.Equal(0f, losingBet.PayoutRatio);

        UserProfile winnerProfile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Spectators[0].Id);
        UserProfile loserProfile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Spectators[1].Id);
        Assert.Equal(135_000L, winnerProfile.Balance);
        Assert.Equal(0L, loserProfile.Balance);

        TakeoutLedger ledger = await db.TakeoutLedgers.AsNoTracking()
            .SingleAsync(l => l.RaceId == fixture.Race.RaceId && l.BetType == BetType.Win);
        Assert.Equal(150_000m, ledger.TotalPool);
        Assert.Equal(15_000m, ledger.TakeoutAmount);
    }

    [Fact]
    public async Task TrySettleAsync_SoleWinningBettor_PayoutRatioFloorsAtOne()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        RaceFixture fixture = await SeedFinishedRaceAsync(db, takeoutPercentage: 0.10f, spectatorCount: 1);

        AddBet(db, fixture.Spectators[0], fixture.Registrations[0], 200_000m, BetType.Win);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());
        await service.TrySettleAsync(fixture.Race.RaceId);

        Bet bet = await db.Bets.AsNoTracking().SingleAsync();
        Assert.Equal(BetStatus.Won, bet.Status);
        Assert.Equal(1.0f, bet.PayoutRatio);

        UserProfile profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Spectators[0].Id);
        Assert.Equal(200_000L, profile.Balance);
    }

    [Fact]
    public async Task TrySettleAsync_NoBetsOnWinner_BetsLostAndPoolBanksAsCarryover()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        RaceFixture fixture = await SeedFinishedRaceAsync(db, takeoutPercentage: 0.10f, spectatorCount: 2);

        AddBet(db, fixture.Spectators[0], fixture.Registrations[1], 50_000m, BetType.Win);
        AddBet(db, fixture.Spectators[1], fixture.Registrations[2], 50_000m, BetType.Win);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());
        await service.TrySettleAsync(fixture.Race.RaceId);

        List<Bet> bets = await db.Bets.AsNoTracking().ToListAsync();
        Assert.All(bets, b => Assert.Equal(BetStatus.Lost, b.Status));
        Assert.All(bets, b => Assert.Equal(0f, b.PayoutRatio));

        Prize winnerOwnerPrize = await db.Prizes.AsNoTracking()
            .SingleAsync(p => p.RegistrationId == fixture.Registrations[0].RegistrationId && p.PrizeType == PrizeType.Owner);
        Assert.Equal(450_000m, winnerOwnerPrize.Amount);

        BetCarryover carryover = await db.BetCarryovers.AsNoTracking()
            .SingleAsync(c => c.RacecourseId == fixture.Race.RacecourseId && c.BetType == BetType.Win);
        Assert.Equal(90_000m, carryover.Amount);
    }

    [Fact]
    public async Task TrySettleAsync_CarryoverFromPriorRace_AppliesToNextWinningPoolAtSameRacecourseThenResets()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();

        RaceFixture raceA = await SeedFinishedRaceAsync(db, takeoutPercentage: 0.10f, spectatorCount: 2);
        AddBet(db, raceA.Spectators[0], raceA.Registrations[1], 50_000m, BetType.Win);
        AddBet(db, raceA.Spectators[1], raceA.Registrations[2], 50_000m, BetType.Win);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());
        await service.TrySettleAsync(raceA.Race.RaceId);

        BetCarryover carryoverAfterRaceA = await db.BetCarryovers.AsNoTracking()
            .SingleAsync(c => c.RacecourseId == raceA.Race.RacecourseId && c.BetType == BetType.Win);
        Assert.Equal(90_000m, carryoverAfterRaceA.Amount);

        RaceFixture raceB = await SeedFinishedRaceAsync(db, takeoutPercentage: 0.10f, spectatorCount: 1, racecourse: raceA.Race.Racecourse);
        AddBet(db, raceB.Spectators[0], raceB.Registrations[0], 100_000m, BetType.Win);
        await db.SaveChangesAsync();

        await service.TrySettleAsync(raceB.Race.RaceId);

        Bet winningBet = await db.Bets.AsNoTracking().SingleAsync(b => b.RegistrationId == raceB.Registrations[0].RegistrationId);
        Assert.Equal(BetStatus.Won, winningBet.Status);
        Assert.Equal(1.8f, winningBet.PayoutRatio);

        UserProfile winnerProfile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == raceB.Spectators[0].Id);
        Assert.Equal(180_000L, winnerProfile.Balance);

        BetCarryover carryoverAfterRaceB = await db.BetCarryovers.AsNoTracking()
            .SingleAsync(c => c.RacecourseId == raceA.Race.RacecourseId && c.BetType == BetType.Win);
        Assert.Equal(0m, carryoverAfterRaceB.Amount);
    }

    [Fact]
    public async Task TrySettleAsync_CarryoverAtOneRacecourse_DoesNotLeakIntoADifferentRacecourse()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();

        RaceFixture raceAtCourseA = await SeedFinishedRaceAsync(db, takeoutPercentage: 0.10f, spectatorCount: 2);
        AddBet(db, raceAtCourseA.Spectators[0], raceAtCourseA.Registrations[1], 50_000m, BetType.Win);
        AddBet(db, raceAtCourseA.Spectators[1], raceAtCourseA.Registrations[2], 50_000m, BetType.Win);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());
        await service.TrySettleAsync(raceAtCourseA.Race.RaceId);

        BetCarryover carryoverAtCourseA = await db.BetCarryovers.AsNoTracking()
            .SingleAsync(c => c.RacecourseId == raceAtCourseA.Race.RacecourseId && c.BetType == BetType.Win);
        Assert.Equal(90_000m, carryoverAtCourseA.Amount);

        RaceFixture raceAtCourseB = await SeedFinishedRaceAsync(db, takeoutPercentage: 0.10f, spectatorCount: 1);
        AddBet(db, raceAtCourseB.Spectators[0], raceAtCourseB.Registrations[0], 100_000m, BetType.Win);
        await db.SaveChangesAsync();

        await service.TrySettleAsync(raceAtCourseB.Race.RaceId);

        Bet winningBetAtCourseB = await db.Bets.AsNoTracking().SingleAsync(b => b.RegistrationId == raceAtCourseB.Registrations[0].RegistrationId);
        Assert.Equal(1.0f, winningBetAtCourseB.PayoutRatio);

        UserProfile winnerProfile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == raceAtCourseB.Spectators[0].Id);
        Assert.Equal(100_000L, winnerProfile.Balance);

        BetCarryover carryoverAtCourseAAfter = await db.BetCarryovers.AsNoTracking()
            .SingleAsync(c => c.RacecourseId == raceAtCourseA.Race.RacecourseId && c.BetType == BetType.Win);
        Assert.Equal(90_000m, carryoverAtCourseAAfter.Amount);
    }

    [Fact]
    public async Task TrySettleAsync_PlaceAndShowBets_SettleIndependentlyOfWinPool()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        RaceFixture fixture = await SeedFinishedRaceAsync(db, takeoutPercentage: 0.10f, spectatorCount: 3);

        AddBet(db, fixture.Spectators[0], fixture.Registrations[1], 100_000m, BetType.Place);
        AddBet(db, fixture.Spectators[1], fixture.Registrations[2], 100_000m, BetType.Place);
        AddBet(db, fixture.Spectators[2], fixture.Registrations[2], 100_000m, BetType.Show);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());
        await service.TrySettleAsync(fixture.Race.RaceId);

        Bet placeWin = await db.Bets.AsNoTracking().SingleAsync(b => b.SpectatorId == fixture.Spectators[0].Id);
        Bet placeLoss = await db.Bets.AsNoTracking().SingleAsync(b => b.SpectatorId == fixture.Spectators[1].Id);
        Bet showWin = await db.Bets.AsNoTracking().SingleAsync(b => b.SpectatorId == fixture.Spectators[2].Id);

        Assert.Equal(BetStatus.Won, placeWin.Status);
        Assert.Equal(BetStatus.Lost, placeLoss.Status);
        Assert.Equal(BetStatus.Won, showWin.Status);

        int ledgerCount = await db.TakeoutLedgers.CountAsync(l => l.RaceId == fixture.Race.RaceId);
        Assert.Equal(2, ledgerCount);
    }
}
