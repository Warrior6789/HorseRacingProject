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
public class RegistrationServiceRejectScratchIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RegistrationServiceRejectScratchIntegrationTests(PostgresContainerFixture fixture)
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
        public required RegistrationService Service;
        public required Account Owner;
        public required Account Jockey;
        public required Horse Horse;
        public required Race Race;
        public required Registration Registration;
        public required UserProfile OwnerProfile;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedAsync(RaceStatus raceStatus, RegistrationStatus registrationStatus, decimal registrationFee = 1_000m)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = "owner@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey = new Account { Id = Guid.NewGuid(), Email = "jockey@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var ownerProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owner.Id, Balance = 0 };

        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = raceStatus,
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            RegistrationFee = registrationFee,
            PrizePool = 5_000m
        };
        var registration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = race.RaceId,
            HorseId = horse.Id,
            JockeyId = jockey.Id,
            Status = registrationStatus
        };

        db.AddRange(racecourse, owner, jockey, horse, ownerProfile, race, registration);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RegistrationService(uow, CreateHubContext());

        return new Fixture
        {
            Db = db,
            Service = service,
            Owner = owner,
            Jockey = jockey,
            Horse = horse,
            Race = race,
            Registration = registration,
            OwnerProfile = ownerProfile
        };
    }

    [Fact]
    public async Task RejectRegistrationAsync_Pending_RefundsOwnerAndDecrementsPrizePool()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, RegistrationStatus.Pending);

        await fixture.Service.RejectRegistrationAsync(fixture.Registration.RegistrationId, fixture.Jockey.Id);

        Registration reg = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Rejected, reg.Status);
        Assert.False(reg.JockeyConfirmation);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(1_000L, ownerProfile.Balance);

        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Equal(4_000m, race.PrizePool);
    }

    [Fact]
    public async Task RejectRegistrationAsync_WrongJockey_ThrowsAndLeavesStateUnchanged()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, RegistrationStatus.Pending);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.RejectRegistrationAsync(fixture.Registration.RegistrationId, Guid.NewGuid()));

        Registration reg = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Pending, reg.Status);
    }

    [Fact]
    public async Task RejectRegistrationAsync_AlreadyConfirmed_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, RegistrationStatus.Confirmed);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.RejectRegistrationAsync(fixture.Registration.RegistrationId, fixture.Jockey.Id));
    }

    [Fact]
    public async Task ScratchHorseAsync_ConfirmedRegistration_RefundsFeeAndActiveBetsAndShrinksPool()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.BettingOpen, RegistrationStatus.Confirmed);

        var spectator = new Account { Id = Guid.NewGuid(), Email = "spectator@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        var spectatorProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = spectator.Id, Balance = 0 };
        var bet = new Bet
        {
            BetId = Guid.NewGuid(),
            SpectatorId = spectator.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            BetAmount = 2_000m,
            BetType = BetType.Win,
            Status = BetStatus.Active
        };
        var pool = new RacePool { RacePoolId = Guid.NewGuid(), RaceId = fixture.Race.RaceId, BetType = BetType.Win, TotalAmount = 2_000m };
        fixture.Db.AddRange(spectator, spectatorProfile, bet, pool);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ScratchHorseAsync(fixture.Registration.RegistrationId);

        Registration reg = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Scratched, reg.Status);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(1_000L, ownerProfile.Balance);

        Bet refundedBet = await fixture.Db.Bets.AsNoTracking().SingleAsync(b => b.BetId == bet.BetId);
        Assert.Equal(BetStatus.Refunded, refundedBet.Status);

        UserProfile spectatorProfileAfter = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == spectator.Id);
        Assert.Equal(2_000L, spectatorProfileAfter.Balance);

        RacePool poolAfter = await fixture.Db.RacePools.AsNoTracking().SingleAsync(p => p.RacePoolId == pool.RacePoolId);
        Assert.Equal(0m, poolAfter.TotalAmount);
    }

    [Theory]
    [InlineData(RaceStatus.Live)]
    [InlineData(RaceStatus.Finished)]
    [InlineData(RaceStatus.Cancelled)]
    public async Task ScratchHorseAsync_RaceInTerminalOrLiveStatus_Throws(RaceStatus raceStatus)
    {
        using Fixture fixture = await SeedAsync(raceStatus, RegistrationStatus.Confirmed);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.ScratchHorseAsync(fixture.Registration.RegistrationId));

        Registration reg = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Confirmed, reg.Status);
    }

    [Fact]
    public async Task ScratchHorseAsync_RegistrationNotConfirmed_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, RegistrationStatus.Pending);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.ScratchHorseAsync(fixture.Registration.RegistrationId));
    }

    [Fact]
    public async Task ScratchHorseAsync_RegistrationNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, RegistrationStatus.Confirmed);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.Service.ScratchHorseAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AdminAcceptRegistrationAsync_ConfirmsRegistrationAndRejectsOtherPendingForSameHorse()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, RegistrationStatus.Pending);

        var otherRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = fixture.Race.RacecourseId,
            Status = RaceStatus.Scheduled,
            StartTime = DateTimeOffset.UtcNow.AddDays(2),
            RegistrationFee = 1_000m,
            PrizePool = 5_000m
        };
        var otherPendingRegistration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = otherRace.RaceId,
            HorseId = fixture.Horse.Id,
            JockeyId = fixture.Jockey.Id,
            Status = RegistrationStatus.Pending
        };
        fixture.Db.AddRange(otherRace, otherPendingRegistration);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.AdminAcceptRegistrationAsync(fixture.Registration.RegistrationId);

        Registration accepted = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Confirmed, accepted.Status);

        Registration rejected = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == otherPendingRegistration.RegistrationId);
        Assert.Equal(RegistrationStatus.Rejected, rejected.Status);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(1_000L, ownerProfile.Balance);
    }

    [Fact]
    public async Task AdminAcceptRegistrationAsync_RaceAtMaxParticipants_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, RegistrationStatus.Pending);
        fixture.Race.MaxParticipants = 1;

        var extraHorse = new Horse { Id = Guid.NewGuid(), OwnerId = fixture.Owner.Id, HorseName = "Extra", Status = HorseStatus.Healthy };
        var extraJockey = new Account { Id = Guid.NewGuid(), Email = "jockey2@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var alreadyConfirmed = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            HorseId = extraHorse.Id,
            JockeyId = extraJockey.Id,
            Status = RegistrationStatus.Confirmed
        };
        fixture.Db.AddRange(extraHorse, extraJockey, alreadyConfirmed);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AdminAcceptRegistrationAsync(fixture.Registration.RegistrationId));

        Registration reg = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Pending, reg.Status);
    }

    [Fact]
    public async Task AdminRejectRegistrationAsync_Pending_RefundsOwnerAndDecrementsPrizePool()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, RegistrationStatus.Pending);

        await fixture.Service.AdminRejectRegistrationAsync(fixture.Registration.RegistrationId);

        Registration reg = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Rejected, reg.Status);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(1_000L, ownerProfile.Balance);

        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Equal(4_000m, race.PrizePool);
    }

    [Fact]
    public async Task AdminRejectRegistrationAsync_AlreadyConfirmed_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, RegistrationStatus.Confirmed);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AdminRejectRegistrationAsync(fixture.Registration.RegistrationId));
    }
}
