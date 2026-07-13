using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingProject.Tests;

[Collection("Postgres")]
public class RaceRefereeServiceIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RaceRefereeServiceIntegrationTests(PostgresContainerFixture fixture)
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

    private class Fixture : IDisposable
    {
        public required HorseRacingDataContext Db;
        public required RaceRefereeService Service;
        public required Racecourse Racecourse;
        public required Race Race;
        public required Account Referee;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedAsync(RaceStatus raceStatus = RaceStatus.Scheduled,
        AccountRole refereeRole = AccountRole.Referee,
        AccountStatus refereeStatus = AccountStatus.Active,
        DateTimeOffset? raceStartTime = null)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var referee = new Account { Id = Guid.NewGuid(), Email = "referee@test.com", PasswordHash = "x", Role = refereeRole, Status = refereeStatus };
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = raceStatus,
            StartTime = raceStartTime ?? DateTimeOffset.UtcNow.AddDays(1)
        };

        db.AddRange(racecourse, referee, race);
        await db.SaveChangesAsync();

        var service = new RaceRefereeService(new UnitofWork(db));

        return new Fixture { Db = db, Service = service, Racecourse = racecourse, Race = race, Referee = referee };
    }

    [Fact]
    public async Task AssignAsync_ValidReferee_AssignsSuccessfully()
    {
        using Fixture fixture = await SeedAsync();

        await fixture.Service.AssignAsync(fixture.Race.RaceId, fixture.Referee.Id);

        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Equal(fixture.Referee.Id, race.RefereeId);
    }

    [Fact]
    public async Task AssignAsync_RaceNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.AssignAsync(Guid.NewGuid(), fixture.Referee.Id));
    }

    [Theory]
    [InlineData(RaceStatus.Live)]
    [InlineData(RaceStatus.Finished)]
    [InlineData(RaceStatus.Cancelled)]
    public async Task AssignAsync_RaceInDisallowedStatus_Throws(RaceStatus raceStatus)
    {
        using Fixture fixture = await SeedAsync(raceStatus: raceStatus);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.AssignAsync(fixture.Race.RaceId, fixture.Referee.Id));
    }

    [Fact]
    public async Task AssignAsync_AccountNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.AssignAsync(fixture.Race.RaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task AssignAsync_AccountNotRefereeRole_Throws()
    {
        using Fixture fixture = await SeedAsync(refereeRole: AccountRole.Jockey);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.AssignAsync(fixture.Race.RaceId, fixture.Referee.Id));
    }

    [Fact]
    public async Task AssignAsync_RefereeAccountNotActive_Throws()
    {
        using Fixture fixture = await SeedAsync(refereeStatus: AccountStatus.Suspended);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.AssignAsync(fixture.Race.RaceId, fixture.Referee.Id));
    }

    [Fact]
    public async Task AssignAsync_RefereeActiveInAnotherRaceAtSameRacecourse_Throws()
    {
        using Fixture fixture = await SeedAsync();

        var otherRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = fixture.Racecourse.Id,
            Status = RaceStatus.BettingOpen,
            RaceNumber = 3,
            RefereeId = fixture.Referee.Id,
            StartTime = DateTimeOffset.UtcNow
        };
        fixture.Db.Add(otherRace);
        await fixture.Db.SaveChangesAsync();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AssignAsync(fixture.Race.RaceId, fixture.Referee.Id));
        Assert.Contains("Race #3", ex.Message);
    }

    [Fact]
    public async Task AssignAsync_CannotTravelInTimeBetweenDifferentRacecourses_Throws()
    {
        DateTimeOffset raceStart = DateTimeOffset.UtcNow.AddDays(1);
        using Fixture fixture = await SeedAsync(raceStartTime: raceStart);

        var otherRacecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Other Track" };
        var otherRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = otherRacecourse.Id,
            Status = RaceStatus.Scheduled,
            RaceNumber = 5,
            RefereeId = fixture.Referee.Id,
            StartTime = raceStart.AddHours(-2)
        };
        fixture.Db.AddRange(otherRacecourse, otherRace);
        await fixture.Db.SaveChangesAsync();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AssignAsync(fixture.Race.RaceId, fixture.Referee.Id));
        Assert.Contains("cannot travel in time", ex.Message);
    }

    [Fact]
    public async Task AssignAsync_EnoughTravelTimeBetweenDifferentRacecourses_Succeeds()
    {
        DateTimeOffset raceStart = DateTimeOffset.UtcNow.AddDays(1);
        using Fixture fixture = await SeedAsync(raceStartTime: raceStart);

        var otherRacecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Other Track" };
        var otherRace = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = otherRacecourse.Id,
            Status = RaceStatus.Scheduled,
            RefereeId = fixture.Referee.Id,
            StartTime = raceStart.AddHours(-5)
        };
        fixture.Db.AddRange(otherRacecourse, otherRace);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.AssignAsync(fixture.Race.RaceId, fixture.Referee.Id);

        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Equal(fixture.Referee.Id, race.RefereeId);
    }

    [Fact]
    public async Task UnassignAsync_AssignedReferee_ClearsRefereeId()
    {
        using Fixture fixture = await SeedAsync();
        await fixture.Service.AssignAsync(fixture.Race.RaceId, fixture.Referee.Id);

        await fixture.Service.UnassignAsync(fixture.Race.RaceId);

        Race race = await fixture.Db.Races.AsNoTracking().SingleAsync(r => r.RaceId == fixture.Race.RaceId);
        Assert.Null(race.RefereeId);
    }

    [Fact]
    public async Task UnassignAsync_NoRefereeAssigned_Throws()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UnassignAsync(fixture.Race.RaceId));
    }

    [Fact]
    public async Task UnassignAsync_RaceLive_Throws()
    {
        using Fixture fixture = await SeedAsync(raceStatus: RaceStatus.Scheduled);
        await fixture.Service.AssignAsync(fixture.Race.RaceId, fixture.Referee.Id);

        fixture.Race.Status = RaceStatus.Live;
        fixture.Db.Update(fixture.Race);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UnassignAsync(fixture.Race.RaceId));
    }

    [Fact]
    public async Task UnassignAsync_RaceNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.UnassignAsync(Guid.NewGuid()));
    }
}
