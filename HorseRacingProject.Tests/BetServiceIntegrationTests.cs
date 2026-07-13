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
public class BetServiceIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public BetServiceIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<HorseRacingDataContext> CreateContextAsync()
    {
        await _fixture.ResetAsync();
        return CreateContextNoReset();
    }

    private HorseRacingDataContext CreateContextNoReset()
    {
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
        public required BetService Service;
        public required Race Race;
        public required Registration Registration;
        public required Account Spectator;
        public required UserProfile SpectatorProfile;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedAsync(RaceStatus raceStatus = RaceStatus.BettingOpen,
        RegistrationStatus registrationStatus = RegistrationStatus.Confirmed,
        long spectatorBalance = 10_000)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = "owner@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey = new Account { Id = Guid.NewGuid(), Email = "jockey@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var spectator = new Account { Id = Guid.NewGuid(), Email = "spectator@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var spectatorProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = spectator.Id, Balance = spectatorBalance };

        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = raceStatus,
            StartTime = DateTimeOffset.UtcNow.AddHours(1)
        };
        var registration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = race.RaceId,
            HorseId = horse.Id,
            JockeyId = jockey.Id,
            Status = registrationStatus
        };

        db.AddRange(racecourse, owner, jockey, spectator, horse, spectatorProfile, race, registration);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new BetService(uow, CreateHubContext());

        return new Fixture
        {
            Db = db,
            Service = service,
            Race = race,
            Registration = registration,
            Spectator = spectator,
            SpectatorProfile = spectatorProfile
        };
    }

    [Fact]
    public async Task PlaceBetAsync_ValidBet_DeductsBalanceAndCreatesPoolRow()
    {
        using Fixture fixture = await SeedAsync(spectatorBalance: 10_000);

        BetResponse response = await fixture.Service.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = fixture.Registration.RegistrationId,
            BetType = "Win",
            BetAmount = 3_000m
        });

        Assert.Equal(BetStatus.Active.ToString(), response.Status);

        UserProfile profile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Spectator.Id);
        Assert.Equal(7_000L, profile.Balance);

        RacePool pool = await fixture.Db.RacePools.AsNoTracking()
            .SingleAsync(p => p.RaceId == fixture.Race.RaceId && p.BetType == BetType.Win);
        Assert.Equal(3_000m, pool.TotalAmount);
    }

    [Fact]
    public async Task PlaceBetAsync_SecondBetSameRaceAndType_AccumulatesExistingPool()
    {
        using Fixture fixture = await SeedAsync(spectatorBalance: 20_000);

        await fixture.Service.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = fixture.Registration.RegistrationId,
            BetType = "Win",
            BetAmount = 3_000m
        });

        var owner2 = new Account { Id = Guid.NewGuid(), Email = "owner2@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey2 = new Account { Id = Guid.NewGuid(), Email = "jockey2@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var horse2 = new Horse { Id = Guid.NewGuid(), OwnerId = owner2.Id, HorseName = "Bolt", Status = HorseStatus.Healthy };
        var registration2 = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            HorseId = horse2.Id,
            JockeyId = jockey2.Id,
            Status = RegistrationStatus.Confirmed
        };
        fixture.Db.AddRange(owner2, jockey2, horse2, registration2);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = registration2.RegistrationId,
            BetType = "Win",
            BetAmount = 2_000m
        });

        RacePool pool = await fixture.Db.RacePools.AsNoTracking()
            .SingleAsync(p => p.RaceId == fixture.Race.RaceId && p.BetType == BetType.Win);
        Assert.Equal(5_000m, pool.TotalAmount);
    }

    [Fact]
    public async Task PlaceBetAsync_RaceNotBettingOpen_ThrowsAndDoesNotDeductBalance()
    {
        using Fixture fixture = await SeedAsync(raceStatus: RaceStatus.BettingClosed);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = fixture.Registration.RegistrationId,
            BetType = "Win",
            BetAmount = 3_000m
        }));

        UserProfile profile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Spectator.Id);
        Assert.Equal(10_000L, profile.Balance);
    }

    [Fact]
    public async Task PlaceBetAsync_RegistrationNotConfirmed_Throws()
    {
        using Fixture fixture = await SeedAsync(registrationStatus: RegistrationStatus.Pending);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = fixture.Registration.RegistrationId,
            BetType = "Win",
            BetAmount = 3_000m
        }));
    }

    [Fact]
    public async Task PlaceBetAsync_InvalidBetType_Throws()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = fixture.Registration.RegistrationId,
            BetType = "Exacta",
            BetAmount = 3_000m
        }));
    }

    [Fact]
    public async Task PlaceBetAsync_BelowMinimumAmount_Throws()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = fixture.Registration.RegistrationId,
            BetType = "Win",
            BetAmount = 500m
        }));
    }

    [Fact]
    public async Task PlaceBetAsync_InsufficientBalance_ThrowsAndLeavesBalanceUnchanged()
    {
        using Fixture fixture = await SeedAsync(spectatorBalance: 1_000);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = fixture.Registration.RegistrationId,
            BetType = "Win",
            BetAmount = 5_000m
        }));

        UserProfile profile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Spectator.Id);
        Assert.Equal(1_000L, profile.Balance);
        Assert.False(await fixture.Db.RacePools.AsNoTracking().AnyAsync(p => p.RaceId == fixture.Race.RaceId));
    }

    [Fact]
    public async Task PlaceBetAsync_DuplicateBetTypeOnSameHorse_Throws()
    {
        using Fixture fixture = await SeedAsync(spectatorBalance: 20_000);

        await fixture.Service.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = fixture.Registration.RegistrationId,
            BetType = "Win",
            BetAmount = 3_000m
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = fixture.Registration.RegistrationId,
            BetType = "Win",
            BetAmount = 2_000m
        }));

        int betCount = await fixture.Db.Bets.CountAsync(b => b.SpectatorId == fixture.Spectator.Id);
        Assert.Equal(1, betCount);
    }

    [Fact]
    public async Task PlaceBetAsync_ConcurrentBetsExceedingBalance_OnlyOneSucceeds()
    {
        using Fixture fixture = await SeedAsync(spectatorBalance: 5_000);

        var owner2 = new Account { Id = Guid.NewGuid(), Email = "owner2@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey2 = new Account { Id = Guid.NewGuid(), Email = "jockey2@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var horse2 = new Horse { Id = Guid.NewGuid(), OwnerId = owner2.Id, HorseName = "Bolt", Status = HorseStatus.Healthy };
        var registration2 = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            HorseId = horse2.Id,
            JockeyId = jockey2.Id,
            Status = RegistrationStatus.Confirmed
        };
        fixture.Db.AddRange(owner2, jockey2, horse2, registration2);
        await fixture.Db.SaveChangesAsync();

        await using HorseRacingDataContext dbA = CreateContextNoReset();
        await using HorseRacingDataContext dbB = CreateContextNoReset();
        var serviceA = new BetService(new UnitofWork(dbA), CreateHubContext());
        var serviceB = new BetService(new UnitofWork(dbB), CreateHubContext());

        Task<BetResponse> betOnHorse1 = serviceA.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = fixture.Registration.RegistrationId,
            BetType = "Win",
            BetAmount = 4_000m
        });
        Task<BetResponse> betOnHorse2 = serviceB.PlaceBetAsync(fixture.Spectator.Id, new PlaceBetRequest
        {
            RegistrationId = registration2.RegistrationId,
            BetType = "Win",
            BetAmount = 4_000m
        });

        var results = await Task.WhenAll(
            betOnHorse1.ContinueWith(t => t.Exception?.InnerException),
            betOnHorse2.ContinueWith(t => t.Exception?.InnerException));

        int successCount = results.Count(ex => ex == null);
        int failureCount = results.Count(ex => ex is InvalidOperationException);
        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        UserProfile profile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Spectator.Id);
        Assert.Equal(1_000L, profile.Balance);

        int totalActiveBets = await fixture.Db.Bets.CountAsync(b => b.SpectatorId == fixture.Spectator.Id && b.Status == BetStatus.Active);
        Assert.Equal(1, totalActiveBets);
    }
}
