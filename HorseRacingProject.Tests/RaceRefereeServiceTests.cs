using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingProject.Tests;

public class RaceRefereeServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static RaceRefereeService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new RaceRefereeService(uow);
    }

    private static Account NewReferee(AccountStatus status = AccountStatus.Active) => new Account
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@test.com",
        PasswordHash = "x",
        Role = AccountRole.Referee,
        Status = status
    };

    private static Race NewRace(Guid racecourseId, RaceStatus status, DateTimeOffset startTime, DateTimeOffset? endTime = null) => new Race
    {
        RaceId = Guid.NewGuid(),
        RacecourseId = racecourseId,
        Status = status,
        StartTime = startTime,
        EndTime = endTime
    };

    [Fact]
    public async Task AssignAsync_RaceNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RaceRefereeService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AssignAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Theory]
    [InlineData(RaceStatus.Live)]
    [InlineData(RaceStatus.Finished)]
    [InlineData(RaceStatus.Cancelled)]
    public async Task AssignAsync_RaceInTerminalStatus_ThrowsInvalidOperation(RaceStatus status)
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Race race = NewRace(racecourse.Id, status, DateTimeOffset.UtcNow.AddHours(2));
        db.Racecourses.Add(racecourse);
        db.Races.Add(race);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(race.RaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task AssignAsync_RefereeAccountNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Race race = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(2));
        db.Racecourses.Add(racecourse);
        db.Races.Add(race);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AssignAsync(race.RaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task AssignAsync_AccountNotReferee_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Race race = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(2));
        var notReferee = new Account { Id = Guid.NewGuid(), Email = "x@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        db.Racecourses.Add(racecourse);
        db.Races.Add(race);
        db.Accounts.Add(notReferee);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(race.RaceId, notReferee.Id));
    }

    [Fact]
    public async Task AssignAsync_RefereeNotActive_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Race race = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(2));
        Account referee = NewReferee(AccountStatus.Suspended);
        db.Racecourses.Add(racecourse);
        db.Races.Add(race);
        db.Accounts.Add(referee);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(race.RaceId, referee.Id));
    }

    [Fact]
    public async Task AssignAsync_RefereeActiveInAnotherRaceAtSameVenue_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Account referee = NewReferee();
        Race newRace = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(3));
        Race otherRace = NewRace(racecourse.Id, RaceStatus.Live, DateTimeOffset.UtcNow.AddMinutes(-10));
        otherRace.RefereeId = referee.Id;
        db.Racecourses.Add(racecourse);
        db.Races.AddRange(newRace, otherRace);
        db.Accounts.Add(referee);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignAsync(newRace.RaceId, referee.Id));
        Assert.Contains("same racecourse", ex.Message);
    }

    [Fact]
    public async Task AssignAsync_RefereeCannotTravelInTime_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourseA = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track A" };
        var racecourseB = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track B" };
        Account referee = NewReferee();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Race otherRace = NewRace(racecourseA.Id, RaceStatus.Scheduled, now.AddHours(1), now.AddHours(1).AddMinutes(30));
        otherRace.RefereeId = referee.Id;
        Race newRace = NewRace(racecourseB.Id, RaceStatus.Scheduled, now.AddHours(2));

        db.Racecourses.AddRange(racecourseA, racecourseB);
        db.Races.AddRange(otherRace, newRace);
        db.Accounts.Add(referee);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignAsync(newRace.RaceId, referee.Id));
        Assert.Contains("cannot travel", ex.Message);
    }

    [Fact]
    public async Task AssignAsync_ValidAssignment_Succeeds()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Account referee = NewReferee();
        Race race = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(2));
        db.Racecourses.Add(racecourse);
        db.Races.Add(race);
        db.Accounts.Add(referee);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        RaceRefereeResponse result = await service.AssignAsync(race.RaceId, referee.Id);

        Assert.Equal(referee.Id, result.RefereeId);
        Race updated = await db.Races.AsNoTracking().SingleAsync(r => r.RaceId == race.RaceId);
        Assert.Equal(referee.Id, updated.RefereeId);
    }

    [Fact]
    public async Task UnassignAsync_RaceLive_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Race race = NewRace(racecourse.Id, RaceStatus.Live, DateTimeOffset.UtcNow.AddMinutes(-5));
        race.RefereeId = Guid.NewGuid();
        db.Racecourses.Add(racecourse);
        db.Races.Add(race);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UnassignAsync(race.RaceId));
    }

    [Fact]
    public async Task UnassignAsync_NoRefereeAssigned_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Race race = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(2));
        db.Racecourses.Add(racecourse);
        db.Races.Add(race);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UnassignAsync(race.RaceId));
    }

    [Fact]
    public async Task UnassignAsync_ValidUnassign_ClearsReferee()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Race race = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(2));
        race.RefereeId = Guid.NewGuid();
        db.Racecourses.Add(racecourse);
        db.Races.Add(race);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        await service.UnassignAsync(race.RaceId);

        Race updated = await db.Races.AsNoTracking().SingleAsync(r => r.RaceId == race.RaceId);
        Assert.Null(updated.RefereeId);
    }

    [Fact]
    public async Task GetByRaceAsync_NoRefereeAssigned_ReturnsNull()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Race race = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(2));
        db.Racecourses.Add(racecourse);
        db.Races.Add(race);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        RaceRefereeResponse? result = await service.GetByRaceAsync(race.RaceId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByRaceAsync_RefereeAssigned_ReturnsResponse()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Account referee = NewReferee();
        Race race = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(2));
        race.RefereeId = referee.Id;
        db.Racecourses.Add(racecourse);
        db.Accounts.Add(referee);
        db.Races.Add(race);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        RaceRefereeResponse? result = await service.GetByRaceAsync(race.RaceId);

        Assert.NotNull(result);
        Assert.Equal(referee.Id, result!.RefereeId);
    }

    [Fact]
    public async Task GetMyAssignedRacesAsync_ReturnsOnlyRacesAssignedToReferee()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        Account referee = NewReferee();
        Account otherReferee = NewReferee();
        Race assignedRace = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(2));
        assignedRace.RefereeId = referee.Id;
        Race otherRace = NewRace(racecourse.Id, RaceStatus.Scheduled, DateTimeOffset.UtcNow.AddHours(3));
        otherRace.RefereeId = otherReferee.Id;
        db.Racecourses.Add(racecourse);
        db.Accounts.AddRange(referee, otherReferee);
        db.Races.AddRange(assignedRace, otherRace);
        await db.SaveChangesAsync();

        RaceRefereeService service = CreateService(db);

        List<RaceResponse> result = await service.GetMyAssignedRacesAsync(referee.Id);

        Assert.Single(result);
        Assert.Equal(assignedRace.RaceId, result[0].RaceId);
    }
}
