using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Middlewares;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HorseRacingProject.Tests;

[Collection("Postgres")]
public class RaceServiceRegisterHorseValidationIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RaceServiceRegisterHorseValidationIntegrationTests(PostgresContainerFixture fixture)
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
        public required Account Owner;
        public required UserProfile OwnerProfile;
        public required Account Jockey;
        public required Horse Horse;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedAsync(RaceStatus raceStatus = RaceStatus.Scheduled,
        HorseStatus horseStatus = HorseStatus.Healthy,
        decimal registrationFee = 1_000m,
        long ownerBalance = 10_000,
        int? maxParticipants = null,
        DateTimeOffset? startTime = null)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = "owner@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var ownerProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owner.Id, Balance = ownerBalance };
        var jockey = new Account { Id = Guid.NewGuid(), Email = "jockey@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var jockeyProfile = new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = jockey.Id };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = horseStatus };

        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = raceStatus,
            StartTime = startTime ?? DateTimeOffset.UtcNow.AddDays(1),
            RegistrationFee = registrationFee,
            MaxParticipants = maxParticipants
        };

        db.AddRange(racecourse, owner, ownerProfile, jockey, jockeyProfile, horse, race);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceService(uow, engine: null!, cloudinaryService: Mock.Of<ICloudinaryService>(), hubContext: CreateHubContext());

        return new Fixture
        {
            Db = db,
            Service = service,
            Racecourse = racecourse,
            Race = race,
            Owner = owner,
            OwnerProfile = ownerProfile,
            Jockey = jockey,
            Horse = horse
        };
    }

    [Fact]
    public async Task RegisterHorseAsync_RaceNotScheduled_Throws()
    {
        using Fixture fixture = await SeedAsync(raceStatus: RaceStatus.BettingOpen);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id }));
    }

    [Fact]
    public async Task RegisterHorseAsync_RaceNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.RegisterHorseAsync(
            Guid.NewGuid(), fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id }));
    }

    [Fact]
    public async Task RegisterHorseAsync_CallerDoesNotOwnHorse_ThrowsForbidden()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, Guid.NewGuid(),
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id }));
    }

    [Theory]
    [InlineData(HorseStatus.Injury)]
    [InlineData(HorseStatus.Resting)]
    [InlineData(HorseStatus.Retired)]
    public async Task RegisterHorseAsync_HorseNotHealthy_Throws(HorseStatus horseStatus)
    {
        using Fixture fixture = await SeedAsync(horseStatus: horseStatus);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id }));
    }

    [Fact]
    public async Task RegisterHorseAsync_JockeyNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task RegisterHorseAsync_AccountIsNotJockeyRole_Throws()
    {
        using Fixture fixture = await SeedAsync();

        var notAJockey = new Account { Id = Guid.NewGuid(), Email = "notjockey@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        fixture.Db.Add(notAJockey);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = notAJockey.Id }));
    }

    [Fact]
    public async Task RegisterHorseAsync_JockeyHasNoProfile_Throws()
    {
        using Fixture fixture = await SeedAsync();

        var jockeyWithoutProfile = new Account { Id = Guid.NewGuid(), Email = "nojockeyprofile@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        fixture.Db.Add(jockeyWithoutProfile);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = jockeyWithoutProfile.Id }));
    }

    [Fact]
    public async Task RegisterHorseAsync_MaxParticipantsReached_Throws()
    {
        using Fixture fixture = await SeedAsync(maxParticipants: 1);

        var otherOwner = new Account { Id = Guid.NewGuid(), Email = "owner2@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var otherHorse = new Horse { Id = Guid.NewGuid(), OwnerId = otherOwner.Id, HorseName = "Bolt", Status = HorseStatus.Healthy };
        var existingRegistration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            HorseId = otherHorse.Id,
            JockeyId = fixture.Jockey.Id,
            Status = RegistrationStatus.Pending
        };
        fixture.Db.AddRange(otherOwner, otherHorse, existingRegistration);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id }));
    }

    [Fact]
    public async Task RegisterHorseAsync_GateNumberAlreadyTaken_Throws()
    {
        using Fixture fixture = await SeedAsync();

        var otherOwner = new Account { Id = Guid.NewGuid(), Email = "owner2@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var otherHorse = new Horse { Id = Guid.NewGuid(), OwnerId = otherOwner.Id, HorseName = "Bolt", Status = HorseStatus.Healthy };
        var existingRegistration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            HorseId = otherHorse.Id,
            JockeyId = fixture.Jockey.Id,
            GateNumber = 3,
            Status = RegistrationStatus.Confirmed
        };
        fixture.Db.AddRange(otherOwner, otherHorse, existingRegistration);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id, GateNumber = 3 }));
    }

    [Fact]
    public async Task RegisterHorseAsync_HorseAlreadyConfirmedInAnotherActiveRace_Throws()
    {
        using Fixture fixture = await SeedAsync();

        var otherRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = fixture.Racecourse.Id,
            Status = RaceStatus.BettingOpen,
            StartTime = DateTimeOffset.UtcNow.AddHours(2)
        };
        var otherJockey = new Account { Id = Guid.NewGuid(), Email = "jockey2@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var confirmedElsewhere = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = otherRace.RaceId,
            HorseId = fixture.Horse.Id,
            JockeyId = otherJockey.Id,
            Status = RegistrationStatus.Confirmed
        };
        fixture.Db.AddRange(otherRace, otherJockey, confirmedElsewhere);
        await fixture.Db.SaveChangesAsync();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id }));
        Assert.Contains("already confirmed in a race", ex.Message);
    }

    [Fact]
    public async Task RegisterHorseAsync_HorsePendingAtDifferentRacecourse_Throws()
    {
        using Fixture fixture = await SeedAsync();

        var otherRacecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Other Track" };
        var otherRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = otherRacecourse.Id,
            Status = RaceStatus.Scheduled,
            StartTime = DateTimeOffset.UtcNow.AddDays(2)
        };
        var otherJockey = new Account { Id = Guid.NewGuid(), Email = "jockey2@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var pendingElsewhere = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = otherRace.RaceId,
            HorseId = fixture.Horse.Id,
            JockeyId = otherJockey.Id,
            Status = RegistrationStatus.Pending
        };
        fixture.Db.AddRange(otherRacecourse, otherRace, otherJockey, pendingElsewhere);
        await fixture.Db.SaveChangesAsync();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id }));
        Assert.Contains("pending registration at another racecourse", ex.Message);
    }

    [Fact]
    public async Task RegisterHorseAsync_OwnerAlreadyRegisteredAnotherHorseInSameRace_Throws()
    {
        using Fixture fixture = await SeedAsync();

        var secondHorse = new Horse { Id = Guid.NewGuid(), OwnerId = fixture.Owner.Id, HorseName = "Bolt", Status = HorseStatus.Healthy };
        var otherJockey = new Account { Id = Guid.NewGuid(), Email = "jockey2@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var existingRegistration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            HorseId = secondHorse.Id,
            JockeyId = otherJockey.Id,
            Status = RegistrationStatus.Pending
        };
        fixture.Db.AddRange(secondHorse, otherJockey, existingRegistration);
        await fixture.Db.SaveChangesAsync();

        var thirdHorse = new Horse { Id = Guid.NewGuid(), OwnerId = fixture.Owner.Id, HorseName = "Flash", Status = HorseStatus.Healthy };
        fixture.Db.Add(thirdHorse);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = thirdHorse.Id, JockeyId = fixture.Jockey.Id }));
    }

    [Fact]
    public async Task RegisterHorseAsync_JockeyAlreadyConfirmedInSameRaceWithAnotherHorse_Throws()
    {
        using Fixture fixture = await SeedAsync();

        var otherOwner = new Account { Id = Guid.NewGuid(), Email = "owner2@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var otherHorse = new Horse { Id = Guid.NewGuid(), OwnerId = otherOwner.Id, HorseName = "Bolt", Status = HorseStatus.Healthy };
        var confirmedInSameRace = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            HorseId = otherHorse.Id,
            JockeyId = fixture.Jockey.Id,
            Status = RegistrationStatus.Confirmed
        };
        fixture.Db.AddRange(otherOwner, otherHorse, confirmedInSameRace);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id }));
    }

    [Fact]
    public async Task RegisterHorseAsync_JockeyTimeConflictWithAnotherConfirmedRace_Throws()
    {
        DateTimeOffset raceStart = DateTimeOffset.UtcNow.AddDays(1);
        using Fixture fixture = await SeedAsync(startTime: raceStart);

        var otherRacecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Other Track" };
        var otherRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = otherRacecourse.Id,
            Status = RaceStatus.Scheduled,
            StartTime = raceStart.AddMinutes(10)
        };
        var otherOwner = new Account { Id = Guid.NewGuid(), Email = "owner2@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var otherHorse = new Horse { Id = Guid.NewGuid(), OwnerId = otherOwner.Id, HorseName = "Bolt", Status = HorseStatus.Healthy };
        var confirmedElsewhere = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = otherRace.RaceId,
            HorseId = otherHorse.Id,
            JockeyId = fixture.Jockey.Id,
            Status = RegistrationStatus.Confirmed
        };
        fixture.Db.AddRange(otherRacecourse, otherRace, otherOwner, otherHorse, confirmedElsewhere);
        await fixture.Db.SaveChangesAsync();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id }));
        Assert.Contains("conflicts in time", ex.Message);
    }

    [Fact]
    public async Task RegisterHorseAsync_InsufficientBalanceForRegistrationFee_Throws()
    {
        using Fixture fixture = await SeedAsync(registrationFee: 1_000m, ownerBalance: 500);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id }));

        UserProfile profile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(500L, profile.Balance);
    }

    [Fact]
    public async Task RegisterHorseAsync_ValidRequest_DeductsFeeIncrementsPrizePoolAndCreatesPendingRegistration()
    {
        using Fixture fixture = await SeedAsync(registrationFee: 1_000m, ownerBalance: 10_000);

        RegistrationResponse response = await fixture.Service.RegisterHorseAsync(
            fixture.Race.RaceId, fixture.Owner.Id,
            new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id, GateNumber = 1 });

        Assert.Equal(RegistrationStatus.Pending.ToString(), response.Status);

        UserProfile profile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(9_000L, profile.Balance);

        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Equal(1_000m, race.PrizePool);

        Registration saved = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Equal(RegistrationStatus.Pending, saved.Status);
        Assert.Equal(true, saved.OwnerConfirmation);
        Assert.Null(saved.JockeyConfirmation);
    }
}
