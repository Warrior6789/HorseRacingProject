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
public class RaceServiceRegistrationIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RaceServiceRegistrationIntegrationTests(PostgresContainerFixture fixture)
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

        var hubContext = new Mock<IHubContext<RaceHub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        return hubContext.Object;
    }

    private class Fixture : IDisposable
    {
        public required HorseRacingDataContext Db;
        public required RaceService Service;
        public required Account Owner;
        public required Account Jockey;
        public required Horse Horse;
        public required Race NewRace;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedAsync(DateTimeOffset finishedRaceEndTime, DateTimeOffset newRaceStartTime)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = "owner@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey = new Account { Id = Guid.NewGuid(), Email = "jockey@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var jockeyProfile = new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = jockey.Id };

        var finishedRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = RaceStatus.Finished,
            StartTime = finishedRaceEndTime.AddMinutes(-30),
            EndTime = finishedRaceEndTime
        };
        var finishedRegistration = new Registration
        {
            RegistrationId = Guid.NewGuid(),
            RaceId = finishedRace.RaceId,
            HorseId = horse.Id,
            JockeyId = jockey.Id,
            Status = RegistrationStatus.Confirmed
        };

        var newRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = RaceStatus.Scheduled,
            StartTime = newRaceStartTime
        };

        db.AddRange(racecourse, owner, jockey, horse, jockeyProfile, finishedRace, finishedRegistration, newRace);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new RaceService(uow, engine: null!, cloudinaryService: Mock.Of<ICloudinaryService>(), hubContext: CreateHubContext());

        return new Fixture { Db = db, Service = service, Owner = owner, Jockey = jockey, Horse = horse, NewRace = newRace };
    }

    [Fact]
    public async Task RegisterHorseAsync_HorseConfirmedInFinishedRace_MoreThan7DaysAgo_AllowsReRegistration()
    {
        using Fixture fixture = await SeedAsync(
            finishedRaceEndTime: DateTimeOffset.UtcNow.AddDays(-10),
            newRaceStartTime: DateTimeOffset.UtcNow.AddDays(1));

        var request = new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id };

        RegistrationResponse result = await fixture.Service.RegisterHorseAsync(fixture.NewRace.RaceId, fixture.Owner.Id, request);

        Assert.NotNull(result);
        Registration saved = await fixture.Db.Registrations.SingleAsync(r => r.RaceId == fixture.NewRace.RaceId);
        Assert.Equal(RegistrationStatus.Pending, saved.Status);
    }

    [Fact]
    public async Task RegisterHorseAsync_HorseFinishedRaceLessThan7DaysAgo_ThrowsRestPeriodError()
    {
        using Fixture fixture = await SeedAsync(
            finishedRaceEndTime: DateTimeOffset.UtcNow.AddDays(-3),
            newRaceStartTime: DateTimeOffset.UtcNow.AddDays(1));

        var request = new RegisterHorseToRaceRequest { HorseId = fixture.Horse.Id, JockeyId = fixture.Jockey.Id };

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.RegisterHorseAsync(fixture.NewRace.RaceId, fixture.Owner.Id, request));

        Assert.Contains("7 days rest", ex.Message);
        Assert.Contains("day(s) remaining", ex.Message);
    }
}
