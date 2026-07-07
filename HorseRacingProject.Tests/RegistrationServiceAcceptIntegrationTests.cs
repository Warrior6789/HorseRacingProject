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
public class RegistrationServiceAcceptIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RegistrationServiceAcceptIntegrationTests(PostgresContainerFixture fixture)
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
        public required Race RaceToAccept;
        public required Registration RegistrationToAccept;
        public required Race OtherRace;
        public required Registration OtherPendingRegistration;
        public required UserProfile OwnerProfile;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedAsync()
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = "owner@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey = new Account { Id = Guid.NewGuid(), Email = "jockey@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var ownerProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owner.Id, Balance = 0 };

        var raceToAccept = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = RaceStatus.Scheduled,
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            RegistrationFee = 1_000m,
            PrizePool = 5_000m
        };
        var registrationToAccept = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = raceToAccept.RaceId,
            HorseId = horse.Id,
            JockeyId = jockey.Id,
            Status = RegistrationStatus.Pending
        };

        var otherRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = RaceStatus.Scheduled,
            StartTime = DateTimeOffset.UtcNow.AddDays(2),
            RegistrationFee = 1_000m,
            PrizePool = 5_000m
        };
        var otherPendingRegistration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = otherRace.RaceId,
            HorseId = horse.Id,
            JockeyId = jockey.Id,
            Status = RegistrationStatus.Pending
        };

        db.AddRange(racecourse, owner, jockey, horse, ownerProfile, raceToAccept, registrationToAccept, otherRace, otherPendingRegistration);
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
            RaceToAccept = raceToAccept,
            RegistrationToAccept = registrationToAccept,
            OtherRace = otherRace,
            OtherPendingRegistration = otherPendingRegistration,
            OwnerProfile = ownerProfile
        };
    }

    [Fact]
    public async Task AcceptRegistrationAsync_ConfirmsRegistration_AndRejectsOtherPendingForSameHorse()
    {
        using Fixture fixture = await SeedAsync();

        await fixture.Service.AcceptRegistrationAsync(fixture.RegistrationToAccept.RegistrationId, fixture.Jockey.Id);

        Registration accepted = await fixture.Db.Registrations.AsNoTracking()
            .SingleAsync(r => r.RegistrationId == fixture.RegistrationToAccept.RegistrationId);
        Assert.Equal(RegistrationStatus.Confirmed, accepted.Status);
        Assert.True(accepted.JockeyConfirmation);

        Registration rejected = await fixture.Db.Registrations.AsNoTracking()
            .SingleAsync(r => r.RegistrationId == fixture.OtherPendingRegistration.RegistrationId);
        Assert.Equal(RegistrationStatus.Rejected, rejected.Status);
        Assert.False(rejected.JockeyConfirmation);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking()
            .SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(1_000L, ownerProfile.Balance);

        Race otherRace = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.OtherRace.RaceId);
        Assert.Equal(4_000m, otherRace.PrizePool);
    }

    [Fact]
    public async Task AcceptRegistrationAsync_RaceAtMaxParticipants_ThrowsAndDoesNotRejectOtherRegistration()
    {
        using Fixture fixture = await SeedAsync();

        fixture.RaceToAccept.MaxParticipants = 1;
        var extraHorse = new Horse { Id = Guid.NewGuid(), OwnerId = fixture.Owner.Id, HorseName = "Extra", Status = HorseStatus.Healthy };
        var extraJockey = new Account { Id = Guid.NewGuid(), Email = "jockey2@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var alreadyConfirmed = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = fixture.RaceToAccept.RaceId,
            HorseId = extraHorse.Id,
            JockeyId = extraJockey.Id,
            Status = RegistrationStatus.Confirmed
        };
        fixture.Db.AddRange(extraHorse, extraJockey, alreadyConfirmed);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AcceptRegistrationAsync(fixture.RegistrationToAccept.RegistrationId, fixture.Jockey.Id));

        Registration stillPending = await fixture.Db.Registrations.AsNoTracking()
            .SingleAsync(r => r.RegistrationId == fixture.OtherPendingRegistration.RegistrationId);
        Assert.Equal(RegistrationStatus.Pending, stillPending.Status);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking()
            .SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(0L, ownerProfile.Balance);
    }
}
