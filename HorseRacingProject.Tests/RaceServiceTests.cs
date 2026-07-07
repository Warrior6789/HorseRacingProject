using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HorseRacingProject.Tests;

public class RaceServiceTests
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

    private static RaceService CreateService(HorseRacingDataContext db, Mock<ICloudinaryService>? cloudinary = null)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new RaceService(uow, engine: null!, (cloudinary ?? new Mock<ICloudinaryService>()).Object, CreateHubContext());
    }

    private static Racecourse NewRacecourse() => new Racecourse { Id = Guid.NewGuid(), RacecourseName = $"Track-{Guid.NewGuid():N}" };

    private static Account NewAccount(AccountRole role) => new Account
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@test.com",
        PasswordHash = "x",
        Role = role,
        Status = AccountStatus.Active
    };

    private static Horse NewHorse(Guid ownerId) => new Horse { Id = Guid.NewGuid(), OwnerId = ownerId, HorseName = "Thunder", Status = HorseStatus.Healthy };

    [Fact]
    public async Task GetRacesAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        db.Racecourses.Add(racecourse);
        for (int i = 0; i < 3; i++)
            db.Races.Add(new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddDays(i) });
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        PagedResponse<RaceResponse> result = await service.GetRacesAsync(page: 0, pageSize: -5, racecourseId: null, status: null);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetRacesAsync_FiltersByStatusAndSearch()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        db.Racecourses.Add(racecourse);
        db.Races.Add(new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, RaceName = "Derby Cup", Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddDays(1) });
        db.Races.Add(new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, RaceName = "Sprint Bowl", Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        PagedResponse<RaceResponse> result = await service.GetRacesAsync(page: 1, pageSize: 10, racecourseId: null, status: "Scheduled", search: "derby");

        Assert.Single(result.Items);
        Assert.Equal("Derby Cup", result.Items[0].RaceName);
    }

    [Fact]
    public async Task GetRaceByIdAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetRaceByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetRaceByIdAsync_WithPendingRefereeReport_HasUnresolvedReportsTrue()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Account referee = NewAccount(AccountRole.Referee);
        Horse horse = NewHorse(owner.Id);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Live, StartTime = DateTimeOffset.UtcNow };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        var report = new RefereeReport { ReportId = Guid.NewGuid(), RaceId = race.RaceId, RefereeId = referee.Id, RegistrationId = registration.RegistrationId, Status = RefereeReportStatus.Pending };
        db.AddRange(racecourse, owner, jockey, referee, horse, race, registration, report);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        RaceResponse result = await service.GetRaceByIdAsync(race.RaceId);

        Assert.True(result.HasUnresolvedReports);
    }

    [Fact]
    public async Task CreateRaceAsync_RacecourseNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);
        var request = new CreateRaceRequest { RacecourseId = Guid.NewGuid(), RaceNumber = 1, StartTime = DateTimeOffset.UtcNow.AddHours(3) };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateRaceAsync(request));
    }

    [Fact]
    public async Task CreateRaceAsync_StartTimeTooSoon_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        db.Racecourses.Add(racecourse);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);
        var request = new CreateRaceRequest { RacecourseId = racecourse.Id, RaceNumber = 1, StartTime = DateTimeOffset.UtcNow.AddMinutes(30) };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRaceAsync(request));
    }

    [Fact]
    public async Task CreateRaceAsync_DuplicateRaceNumber_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        db.Racecourses.Add(racecourse);
        db.Races.Add(new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, RaceNumber = 1, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(5) });
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);
        var request = new CreateRaceRequest { RacecourseId = racecourse.Id, RaceNumber = 1, StartTime = DateTimeOffset.UtcNow.AddHours(3) };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRaceAsync(request));
    }

    [Fact]
    public async Task CreateRaceAsync_ConflictingTimeSlot_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        db.Racecourses.Add(racecourse);
        DateTimeOffset existingStart = DateTimeOffset.UtcNow.AddHours(3);
        db.Races.Add(new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, RaceNumber = 1, Status = RaceStatus.Scheduled, StartTime = existingStart });
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);
        var request = new CreateRaceRequest { RacecourseId = racecourse.Id, RaceNumber = 2, StartTime = existingStart.AddMinutes(5) };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRaceAsync(request));
    }

    [Fact]
    public async Task CreateRaceAsync_Valid_CreatesScheduledRaceWithActiveFeeConfig()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        db.Racecourses.Add(racecourse);
        db.RegistrationFeeConfigs.Add(new RegistrationFeeConfig { RegistrationFeeConfigId = Guid.NewGuid(), FeeAmount = 20_000, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);
        var request = new CreateRaceRequest { RacecourseId = racecourse.Id, RaceNumber = 1, RaceName = "Grand Prix", StartTime = DateTimeOffset.UtcNow.AddHours(3) };

        RaceResponse result = await service.CreateRaceAsync(request);

        Assert.Equal(RaceStatus.Scheduled.ToString(), result.Status);
        Assert.Equal(20_000, result.RegistrationFee);
        Assert.Equal("Grand Prix", result.RaceName);
    }

    [Fact]
    public async Task UpdateRaceAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateRaceAsync(Guid.NewGuid(), new UpdateRaceRequest()));
    }

    [Fact]
    public async Task UpdateRaceAsync_NotScheduled_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Live, StartTime = DateTimeOffset.UtcNow };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateRaceAsync(race.RaceId, new UpdateRaceRequest { RaceName = "New" }));
    }

    [Fact]
    public async Task UpdateRaceAsync_DuplicateRaceNumber_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, RaceNumber = 1, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(3) };
        var otherRace = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, RaceNumber = 2, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(6) };
        db.AddRange(racecourse, race, otherRace);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateRaceAsync(race.RaceId, new UpdateRaceRequest { RaceNumber = 2 }));
    }

    [Fact]
    public async Task UpdateRaceAsync_Valid_UpdatesFields()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, RaceNumber = 1, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(3) };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        RaceResponse result = await service.UpdateRaceAsync(race.RaceId, new UpdateRaceRequest { RaceName = "Updated Name", TrackLength = 1600 });

        Assert.Equal("Updated Name", result.RaceName);
        Assert.Equal(1600, result.TrackLength);
    }

    [Fact]
    public async Task DeleteRaceAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteRaceAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteRaceAsync_LiveStatus_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Live, StartTime = DateTimeOffset.UtcNow };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRaceAsync(race.RaceId));
    }

    [Fact]
    public async Task DeleteRaceAsync_Valid_SoftDeletesAndRefundsPendingRegistrations()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Horse horse = NewHorse(owner.Id);
        var profile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owner.Id, Balance = 0 };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(3), RegistrationFee = 15_000 };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Pending };
        db.AddRange(racecourse, owner, jockey, horse, profile, race, registration);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        await service.DeleteRaceAsync(race.RaceId);

        Race updatedRace = await db.Races.AsNoTracking().SingleAsync(r => r.RaceId == race.RaceId);
        Registration updatedReg = await db.Registrations.AsNoTracking().SingleAsync(r => r.RegistrationId == registration.RegistrationId);
        UserProfile updatedProfile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == owner.Id);
        Assert.True(updatedRace.IsDeleted);
        Assert.Equal(RegistrationStatus.Rejected, updatedReg.Status);
        Assert.Equal(15_000, updatedProfile.Balance);
    }

    [Fact]
    public async Task GetUpcomingRacesAsync_DefaultStatuses_ExcludesFinishedAndCancelled()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        db.Racecourses.Add(racecourse);
        db.Races.Add(new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(1) });
        db.Races.Add(new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow.AddHours(-1) });
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        PagedResponse<UpcomingRaceResponse> result = await service.GetUpcomingRacesAsync(page: 1, pageSize: 10, statuses: null);

        Assert.Single(result.Items);
        Assert.Equal("Scheduled", result.Items[0].Status);
    }

    [Fact]
    public async Task GetUpcomingRacesAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        db.Racecourses.Add(racecourse);
        db.Races.Add(new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(1) });
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        PagedResponse<UpcomingRaceResponse> result = await service.GetUpcomingRacesAsync(page: 0, pageSize: -5, statuses: null);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task GetRaceResultsAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetRaceResultsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetRaceResultsAsync_ReturnsResultsOrderedByPosition()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Horse horse1 = NewHorse(owner.Id);
        Horse horse2 = NewHorse(owner.Id);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow.AddHours(-2) };
        var reg1 = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse1.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        var reg2 = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse2.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        var result2 = new RaceResult { ResultId = Guid.NewGuid(), RegistrationId = reg2.RegistrationId, FinishPosition = 2 };
        var result1 = new RaceResult { ResultId = Guid.NewGuid(), RegistrationId = reg1.RegistrationId, FinishPosition = 1 };
        db.AddRange(racecourse, owner, jockey, horse1, horse2, race, reg1, reg2, result2, result1);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        List<RaceResultResponse> results = await service.GetRaceResultsAsync(race.RaceId);

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Position);
        Assert.Equal(2, results[1].Position);
    }

    [Fact]
    public async Task GetRaceHorsesAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetRaceHorsesAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetRaceHorsesAsync_OnlyReturnsConfirmedRegistrations()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Horse confirmedHorse = NewHorse(owner.Id);
        Horse pendingHorse = NewHorse(owner.Id);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        var confirmedReg = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = confirmedHorse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        var pendingReg = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = pendingHorse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Pending };
        db.AddRange(racecourse, owner, jockey, confirmedHorse, pendingHorse, race, confirmedReg, pendingReg);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        List<RaceResultHorseDto> horses = await service.GetRaceHorsesAsync(race.RaceId);

        Assert.Single(horses);
        Assert.Equal(confirmedHorse.Id, horses[0].Id);
    }

    [Fact]
    public async Task GetRaceRegistrationsAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetRaceRegistrationsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetRaceRegistrationsAsync_OnlyReturnsConfirmedRegistrations()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Horse horse = NewHorse(owner.Id);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        var confirmedReg = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed, GateNumber = 1 };
        db.AddRange(racecourse, owner, jockey, horse, race, confirmedReg);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        List<RegistrationResponse> registrations = await service.GetRaceRegistrationsAsync(race.RaceId);

        Assert.Single(registrations);
        Assert.Equal(confirmedReg.RegistrationId, registrations[0].RegistrationId);
    }

    [Fact]
    public async Task ResetRaceAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ResetRaceAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AdvanceRaceStatusAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_FinishedStatus_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow.AddHours(-1) };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdvanceRaceStatusAsync(race.RaceId));
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_ScheduledToBettingOpen_MissingConfigs_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(1) };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdvanceRaceStatusAsync(race.RaceId));
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_ScheduledToBettingOpen_Valid_SetsConfigsAndStatus()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(1) };
        var posConfig = new PositionPrizeConfig { PositionPrizeConfigId = Guid.NewGuid(), Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        var jockeyConfig = new JockeyRewardConfig { JockeyRewardConfigId = Guid.NewGuid(), Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.AddRange(racecourse, race, posConfig, jockeyConfig);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        RaceResponse result = await service.AdvanceRaceStatusAsync(race.RaceId);

        Assert.Equal(RaceStatus.BettingOpen.ToString(), result.Status);
        Race updated = await db.Races.AsNoTracking().SingleAsync(r => r.RaceId == race.RaceId);
        Assert.Equal(posConfig.PositionPrizeConfigId, updated.PositionPrizeConfigId);
        Assert.Equal(jockeyConfig.JockeyRewardConfigId, updated.JockeyRewardConfigId);
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_BettingClosedToLive_LessThan3Confirmed_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account referee = NewAccount(AccountRole.Referee);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.BettingClosed, StartTime = DateTimeOffset.UtcNow.AddMinutes(5), RefereeId = referee.Id };
        db.AddRange(racecourse, referee, race);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdvanceRaceStatusAsync(race.RaceId));
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_BettingClosedToLive_NoReferee_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.BettingClosed, StartTime = DateTimeOffset.UtcNow.AddMinutes(5) };
        db.AddRange(racecourse, owner, jockey, race);
        for (int i = 0; i < 3; i++)
        {
            Horse h = NewHorse(owner.Id);
            db.Horses.Add(h);
            db.Registrations.Add(new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = h.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed });
        }
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdvanceRaceStatusAsync(race.RaceId));
    }

    [Fact]
    public async Task AdvanceRaceStatusAsync_BettingClosedToLive_Valid_SetsLive()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Account referee = NewAccount(AccountRole.Referee);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.BettingClosed, StartTime = DateTimeOffset.UtcNow.AddMinutes(5), RefereeId = referee.Id };
        db.AddRange(racecourse, owner, jockey, referee, race);
        for (int i = 0; i < 3; i++)
        {
            Horse h = NewHorse(owner.Id);
            db.Horses.Add(h);
            db.Registrations.Add(new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = h.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed });
        }
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        RaceResponse result = await service.AdvanceRaceStatusAsync(race.RaceId);

        Assert.Equal(RaceStatus.Live.ToString(), result.Status);
    }

    [Fact]
    public async Task UploadImageAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UploadImageAsync(Guid.NewGuid(), Mock.Of<IFormFile>()));
    }

    [Fact]
    public async Task UploadImageAsync_Valid_UpdatesImageUrlAndDeletesOldImage()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(1), ImageUrl = "https://cdn.test/old/abc.jpg" };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();

        var mockCloudinary = new Mock<ICloudinaryService>();
        mockCloudinary.Setup(c => c.UploadImageAsync(It.IsAny<IFormFile>(), "races")).ReturnsAsync("https://cdn.test/new/def.jpg");
        RaceService service = CreateService(db, mockCloudinary);

        string url = await service.UploadImageAsync(race.RaceId, Mock.Of<IFormFile>());

        Assert.Equal("https://cdn.test/new/def.jpg", url);
        mockCloudinary.Verify(c => c.DeleteImageAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CollectFromSpectatorsAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CollectFromSpectatorsAsync(Guid.NewGuid(), new CollectToRacePoolRequest { AmountPerSpectator = 1000, BetType = BetType.Win }));
    }

    [Fact]
    public async Task CollectFromSpectatorsAsync_NotBettingClosed_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(1) };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CollectFromSpectatorsAsync(race.RaceId, new CollectToRacePoolRequest { AmountPerSpectator = 1000, BetType = BetType.Win }));
    }

    [Fact]
    public async Task CollectFromSpectatorsAsync_AmountNotPositive_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.BettingClosed, StartTime = DateTimeOffset.UtcNow.AddMinutes(5) };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CollectFromSpectatorsAsync(race.RaceId, new CollectToRacePoolRequest { AmountPerSpectator = 0, BetType = BetType.Win }));
    }

    [Fact]
    public async Task CollectFromSpectatorsAsync_Valid_ChargesEligibleSpectatorsAndSkipsInsufficientBalance()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.BettingClosed, StartTime = DateTimeOffset.UtcNow.AddMinutes(5) };
        Account richSpectator = NewAccount(AccountRole.Spectator);
        Account poorSpectator = NewAccount(AccountRole.Spectator);
        var richProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = richSpectator.Id, Balance = 5000 };
        var poorProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = poorSpectator.Id, Balance = 100 };
        db.AddRange(racecourse, race, richSpectator, poorSpectator, richProfile, poorProfile);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        CollectToRacePoolResponse result = await service.CollectFromSpectatorsAsync(race.RaceId, new CollectToRacePoolRequest { AmountPerSpectator = 1000, BetType = BetType.Win });

        Assert.Equal(1, result.ChargedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(1000, result.TotalCollected);

        UserProfile updatedRich = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == richSpectator.Id);
        Assert.Equal(4000, updatedRich.Balance);
    }

    [Fact]
    public async Task GetRacePoolOverviewAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetRacePoolOverviewAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetRacePoolOverviewAsync_ReturnsPoolsAndBets()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Account spectator = NewAccount(AccountRole.Spectator);
        Horse horse = NewHorse(owner.Id);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.BettingClosed, StartTime = DateTimeOffset.UtcNow.AddMinutes(5) };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        var pool = new RacePool { RacePoolId = Guid.NewGuid(), RaceId = race.RaceId, BetType = BetType.Win, TotalAmount = 10_000 };
        var bet = new Bet { BetId = Guid.NewGuid(), SpectatorId = spectator.Id, RegistrationId = registration.RegistrationId, BetAmount = 10_000, BetType = BetType.Win, Status = BetStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.AddRange(racecourse, owner, jockey, spectator, horse, race, registration, pool, bet);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        RacePoolOverviewResponse result = await service.GetRacePoolOverviewAsync(race.RaceId);

        Assert.Equal(10_000, result.TotalPoolAmount);
        Assert.Single(result.Pools);
        Assert.Single(result.Bets);
    }

    [Fact]
    public async Task GetPrizePreviewAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetPrizePreviewAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPrizePreviewAsync_AlreadySettled_ReturnsFinalTrue()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Horse horse = NewHorse(owner.Id);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow.AddHours(-2) };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        var result = new RaceResult { ResultId = Guid.NewGuid(), RegistrationId = registration.RegistrationId, FinishPosition = 1 };
        var prize = new Prize { PrizeId = Guid.NewGuid(), RegistrationId = registration.RegistrationId, PrizeType = PrizeType.Owner, Amount = 100_000 };
        db.AddRange(racecourse, owner, jockey, horse, race, registration, result, prize);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        RacePrizePreviewResponse preview = await service.GetPrizePreviewAsync(race.RaceId);

        Assert.True(preview.IsFinal);
        Assert.Single(preview.Items);
        Assert.Equal(100_000, preview.RacePurse);
    }

    [Fact]
    public async Task GetPrizePreviewAsync_NotSettledNoResults_ReturnsNotFinalEmpty()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Live, StartTime = DateTimeOffset.UtcNow, PrizePool = 50_000 };
        db.AddRange(racecourse, race);
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        RacePrizePreviewResponse preview = await service.GetPrizePreviewAsync(race.RaceId);

        Assert.False(preview.IsFinal);
        Assert.Empty(preview.Items);
        Assert.Equal(50_000, preview.RacePurse);
    }

    [Fact]
    public async Task GetTakeoutLedgerPagedAsync_InvalidBetType_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTakeoutLedgerPagedAsync(page: 1, pageSize: 10, raceId: null, betType: "NotARealType"));
    }

    [Fact]
    public async Task GetTakeoutLedgerPagedAsync_FiltersByRaceIdAndSumsTakeout()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow.AddHours(-2) };
        var otherRace = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow.AddHours(-3) };
        db.AddRange(racecourse, race, otherRace);
        db.TakeoutLedgers.Add(new TakeoutLedger { TakeoutLedgerId = Guid.NewGuid(), RaceId = race.RaceId, BetType = BetType.Win, TotalPool = 10_000, TakeoutPercentage = 0.2f, TakeoutAmount = 2000, CreatedAt = DateTimeOffset.UtcNow });
        db.TakeoutLedgers.Add(new TakeoutLedger { TakeoutLedgerId = Guid.NewGuid(), RaceId = otherRace.RaceId, BetType = BetType.Win, TotalPool = 5000, TakeoutPercentage = 0.2f, TakeoutAmount = 1000, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        RaceService service = CreateService(db);

        TakeoutLedgerPagedResponse result = await service.GetTakeoutLedgerPagedAsync(page: 1, pageSize: 10, raceId: race.RaceId, betType: null);

        Assert.Single(result.Items);
        Assert.Equal(2000, result.TotalTakeoutAmount);
    }
}
