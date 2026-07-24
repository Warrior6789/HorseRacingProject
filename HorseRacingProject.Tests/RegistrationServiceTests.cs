using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace HorseRacingProject.Tests;

public class RegistrationServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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

    private static RegistrationService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new RegistrationService(uow, CreateHubContext());
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

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Fixture> SeedAsync(RegistrationStatus regStatus = RegistrationStatus.Pending, decimal registrationFee = 0, int? maxParticipants = null)
    {
        HorseRacingDataContext db = CreateContext();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = RaceStatus.Scheduled,
            StartTime = DateTimeOffset.UtcNow.AddHours(2),
            RegistrationFee = registrationFee,
            PrizePool = registrationFee,
            MaxParticipants = maxParticipants
        };
        var registration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = race.RaceId,
            HorseId = horse.Id,
            JockeyId = jockey.Id,
            Status = regStatus,
            OwnerConfirmation = true
        };

        db.AddRange(racecourse, owner, jockey, horse, race, registration);
        if (registrationFee > 0)
            db.UserProfiles.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owner.Id, Balance = 0 });
        await db.SaveChangesAsync();

        return new Fixture { Db = db, Service = CreateService(db), Owner = owner, Jockey = jockey, Horse = horse, Race = race, Registration = registration };
    }

    [Fact]
    public async Task AcceptRegistrationAsync_NotFound_ThrowsArgumentException()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.AcceptRegistrationAsync(Guid.NewGuid(), fixture.Jockey.Id));
    }

    [Fact]
    public async Task AcceptRegistrationAsync_WrongJockey_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AcceptRegistrationAsync(fixture.Registration.RegistrationId, Guid.NewGuid()));
    }

    [Fact]
    public async Task AcceptRegistrationAsync_NotPending_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync(regStatus: RegistrationStatus.Confirmed);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AcceptRegistrationAsync(fixture.Registration.RegistrationId, fixture.Jockey.Id));
    }

    [Fact]
    public async Task AcceptRegistrationAsync_MaxParticipantsReached_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync(maxParticipants: 1);
        Account otherOwner = new() { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        Horse otherHorse = new() { Id = Guid.NewGuid(), OwnerId = otherOwner.Id, HorseName = "Bolt", Status = HorseStatus.Healthy };
        Registration alreadyConfirmed = new()
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            HorseId = otherHorse.Id,
            JockeyId = Guid.NewGuid(),
            Status = RegistrationStatus.Confirmed
        };
        fixture.Db.AddRange(otherOwner, otherHorse, alreadyConfirmed);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AcceptRegistrationAsync(fixture.Registration.RegistrationId, fixture.Jockey.Id));
    }

    [Fact]
    public async Task AcceptRegistrationAsync_Valid_ConfirmsRegistration()
    {
        using Fixture fixture = await SeedAsync();

        await fixture.Service.AcceptRegistrationAsync(fixture.Registration.RegistrationId, fixture.Jockey.Id);

        Registration updated = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Confirmed, updated.Status);
        Assert.True(updated.JockeyConfirmation);
    }

    [Fact]
    public async Task AcceptRegistrationAsync_RejectsOtherPendingRegistrationsForSameHorse_AndRefundsFee()
    {
        using Fixture fixture = await SeedAsync(registrationFee: 50_000);

        var racecourse2 = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track2" };
        var otherRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse2.Id,
            Status = RaceStatus.Scheduled,
            StartTime = DateTimeOffset.UtcNow.AddHours(5),
            RegistrationFee = 50_000,
            PrizePool = 50_000
        };
        var otherPendingReg = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = otherRace.RaceId,
            HorseId = fixture.Horse.Id,
            JockeyId = Guid.NewGuid(),
            Status = RegistrationStatus.Pending
        };
        fixture.Db.AddRange(racecourse2, otherRace, otherPendingReg);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.AcceptRegistrationAsync(fixture.Registration.RegistrationId, fixture.Jockey.Id);

        Registration updatedOther = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == otherPendingReg.RegistrationId);
        Assert.Equal(RegistrationStatus.Rejected, updatedOther.Status);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(50_000, ownerProfile.Balance);
    }

    [Fact]
    public async Task RejectRegistrationAsync_NotFound_ThrowsArgumentException()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.RejectRegistrationAsync(Guid.NewGuid(), fixture.Jockey.Id));
    }

    [Fact]
    public async Task RejectRegistrationAsync_WrongJockey_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.RejectRegistrationAsync(fixture.Registration.RegistrationId, Guid.NewGuid()));
    }

    [Fact]
    public async Task RejectRegistrationAsync_Valid_RejectsAndRefundsFee()
    {
        using Fixture fixture = await SeedAsync(registrationFee: 30_000);

        await fixture.Service.RejectRegistrationAsync(fixture.Registration.RegistrationId, fixture.Jockey.Id);

        Registration updated = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Rejected, updated.Status);
        Assert.False(updated.JockeyConfirmation);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(30_000, ownerProfile.Balance);
    }

    [Fact]
    public async Task AdminAcceptRegistrationAsync_NotPending_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync(regStatus: RegistrationStatus.Rejected);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AdminAcceptRegistrationAsync(fixture.Registration.RegistrationId));
    }

    [Fact]
    public async Task AdminAcceptRegistrationAsync_Valid_ConfirmsRegistration()
    {
        using Fixture fixture = await SeedAsync();

        await fixture.Service.AdminAcceptRegistrationAsync(fixture.Registration.RegistrationId);

        Registration updated = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Confirmed, updated.Status);
    }

    [Fact]
    public async Task AdminRejectRegistrationAsync_Valid_RejectsRegistration()
    {
        using Fixture fixture = await SeedAsync();

        await fixture.Service.AdminRejectRegistrationAsync(fixture.Registration.RegistrationId);

        Registration updated = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Rejected, updated.Status);
    }

    [Fact]
    public async Task ScratchHorseAsync_NotFound_ThrowsKeyNotFound()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.ScratchHorseAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ScratchHorseAsync_NotConfirmed_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync(regStatus: RegistrationStatus.Pending);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ScratchHorseAsync(fixture.Registration.RegistrationId));
    }

    [Theory]
    [InlineData(RaceStatus.Live)]
    [InlineData(RaceStatus.Finished)]
    [InlineData(RaceStatus.Cancelled)]
    public async Task ScratchHorseAsync_RaceInTerminalStatus_ThrowsInvalidOperation(RaceStatus status)
    {
        using Fixture fixture = await SeedAsync(regStatus: RegistrationStatus.Confirmed);
        fixture.Race.Status = status;
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ScratchHorseAsync(fixture.Registration.RegistrationId));
    }

    [Fact]
    public async Task ScratchHorseAsync_Valid_ScratchesAndRefundsFee_NoActiveBets()
    {
        using Fixture fixture = await SeedAsync(regStatus: RegistrationStatus.Confirmed, registrationFee: 40_000);

        await fixture.Service.ScratchHorseAsync(fixture.Registration.RegistrationId);

        Registration updated = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(RegistrationStatus.Scratched, updated.Status);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(40_000, ownerProfile.Balance);

        Race updatedRace = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Equal(0, updatedRace.PrizePool);
    }
}
