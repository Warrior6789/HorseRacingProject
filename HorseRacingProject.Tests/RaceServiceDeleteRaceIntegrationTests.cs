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
public class RaceServiceDeleteRaceIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RaceServiceDeleteRaceIntegrationTests(PostgresContainerFixture fixture)
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
        public required Account Owner;
        public required Registration PendingRegistration;
        public required Registration ConfirmedRegistration;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedAsync(RaceStatus raceStatus, decimal registrationFee = 1_000m)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = "owner@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var ownerProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owner.Id, Balance = 0 };
        var jockey = new Account { Id = Guid.NewGuid(), Email = "jockey@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var horse1 = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var horse2 = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Bolt", Status = HorseStatus.Healthy };

        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = raceStatus,
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            RegistrationFee = registrationFee
        };
        var pendingRegistration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = race.RaceId,
            HorseId = horse1.Id,
            JockeyId = jockey.Id,
            Status = RegistrationStatus.Pending
        };
        var confirmedRegistration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = race.RaceId,
            HorseId = horse2.Id,
            JockeyId = jockey.Id,
            Status = RegistrationStatus.Confirmed
        };

        db.AddRange(racecourse, owner, ownerProfile, jockey, horse1, horse2, race, pendingRegistration, confirmedRegistration);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceService(uow, engine: null!, cloudinaryService: Mock.Of<ICloudinaryService>(), hubContext: CreateHubContext());

        return new Fixture
        {
            Db = db,
            Service = service,
            Race = race,
            Owner = owner,
            PendingRegistration = pendingRegistration,
            ConfirmedRegistration = confirmedRegistration
        };
    }

    [Fact]
    public async Task DeleteRaceAsync_WithFee_SoftDeletesRaceRejectsRegistrationsAndRefundsOwner()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Scheduled, registrationFee: 1_000m);

        await fixture.Service.DeleteRaceAsync(fixture.Race.RaceId);

        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.True(race.IsDeleted);
        Assert.NotNull(race.DeletedAt);

        Registration pending = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.PendingRegistration.RegistrationId);
        Assert.Equal(RegistrationStatus.Rejected, pending.Status);

        Registration confirmed = await fixture.Db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.ConfirmedRegistration.RegistrationId);
        Assert.Equal(RegistrationStatus.Rejected, confirmed.Status);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(2_000L, ownerProfile.Balance);
    }

    [Fact]
    public async Task DeleteRaceAsync_RaceIsLive_ThrowsAndLeavesRaceIntact()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Live);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.DeleteRaceAsync(fixture.Race.RaceId));

        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.False(race.IsDeleted);
    }

    [Fact]
    public async Task DeleteRaceAsync_RaceNotFound_Throws()
    {
        HorseRacingDataContext db = await CreateContextAsync();
        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceService(uow, engine: null!, cloudinaryService: Mock.Of<ICloudinaryService>(), hubContext: CreateHubContext());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteRaceAsync(Guid.NewGuid()));
    }
}
