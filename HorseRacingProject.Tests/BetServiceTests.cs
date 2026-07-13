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

public class BetServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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

    private static BetService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new BetService(uow, CreateHubContext());
    }

    private class Fixture : IDisposable
    {
        public required HorseRacingDataContext Db;
        public required BetService Service;
        public required Account Spectator;
        public required Registration Registration;
        public required Race Race;

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Fixture> SeedAsync(RaceStatus raceStatus = RaceStatus.BettingOpen, RegistrationStatus regStatus = RegistrationStatus.Confirmed, bool withProfile = true)
    {
        HorseRacingDataContext db = CreateContext();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var spectator = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = raceStatus, StartTime = DateTimeOffset.UtcNow.AddHours(1) };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = regStatus };

        db.AddRange(racecourse, owner, jockey, spectator, horse, race, registration);
        if (withProfile)
            db.UserProfiles.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = spectator.Id, Balance = 1_000_000 });
        await db.SaveChangesAsync();

        return new Fixture { Db = db, Service = CreateService(db), Spectator = spectator, Registration = registration, Race = race };
    }

    [Fact]
    public async Task PlaceBetAsync_RegistrationNotFound_ThrowsKeyNotFound()
    {
        using Fixture fixture = await SeedAsync();
        var request = new PlaceBetRequest { RegistrationId = Guid.NewGuid(), BetType = "Win", BetAmount = 10_000 };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, request));
    }

    [Fact]
    public async Task PlaceBetAsync_RegistrationNotConfirmed_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync(regStatus: RegistrationStatus.Pending);
        var request = new PlaceBetRequest { RegistrationId = fixture.Registration.RegistrationId, BetType = "Win", BetAmount = 10_000 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, request));
    }

    [Fact]
    public async Task PlaceBetAsync_BettingNotOpen_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync(raceStatus: RaceStatus.Scheduled);
        var request = new PlaceBetRequest { RegistrationId = fixture.Registration.RegistrationId, BetType = "Win", BetAmount = 10_000 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, request));
    }

    [Fact]
    public async Task PlaceBetAsync_InvalidBetType_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync();
        var request = new PlaceBetRequest { RegistrationId = fixture.Registration.RegistrationId, BetType = "Quinella", BetAmount = 10_000 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, request));
    }

    [Fact]
    public async Task PlaceBetAsync_BelowMinimumAmount_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync();
        var request = new PlaceBetRequest { RegistrationId = fixture.Registration.RegistrationId, BetType = "Win", BetAmount = 500 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, request));
    }

    [Fact]
    public async Task PlaceBetAsync_SpectatorHasNoProfile_ThrowsKeyNotFound()
    {
        using Fixture fixture = await SeedAsync(withProfile: false);
        var request = new PlaceBetRequest { RegistrationId = fixture.Registration.RegistrationId, BetType = "Win", BetAmount = 10_000 };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, request));
    }

    [Fact]
    public async Task PlaceBetAsync_DuplicateActiveBet_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync();
        fixture.Db.Bets.Add(new Bet
        {
            BetId = Guid.NewGuid(),
            SpectatorId = fixture.Spectator.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            BetAmount = 5_000,
            BetType = BetType.Win,
            Status = BetStatus.Active
        });
        await fixture.Db.SaveChangesAsync();

        var request = new PlaceBetRequest { RegistrationId = fixture.Registration.RegistrationId, BetType = "Win", BetAmount = 10_000 };

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.PlaceBetAsync(fixture.Spectator.Id, request));
        Assert.Contains("already placed", ex.Message);
    }

    [Fact]
    public async Task GetMyBetsPagedAsync_ClampsInvalidPageAndPageSize_FiltersBySpectator()
    {
        using Fixture fixture = await SeedAsync();
        Account otherSpectator = new() { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        fixture.Db.Accounts.Add(otherSpectator);
        for (int i = 0; i < 3; i++)
        {
            fixture.Db.Bets.Add(new Bet
            {
                BetId = Guid.NewGuid(),
                SpectatorId = fixture.Spectator.Id,
                RegistrationId = fixture.Registration.RegistrationId,
                BetAmount = 10_000,
                BetType = BetType.Win,
                Status = BetStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i)
            });
        }
        fixture.Db.Bets.Add(new Bet
        {
            BetId = Guid.NewGuid(),
            SpectatorId = otherSpectator.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            BetAmount = 10_000,
            BetType = BetType.Win,
            Status = BetStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await fixture.Db.SaveChangesAsync();

        PagedResponse<BetResponse> result = await fixture.Service.GetMyBetsPagedAsync(fixture.Spectator.Id, page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }
}
