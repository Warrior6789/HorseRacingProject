using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HorseRacingProject.Tests;

[Collection("Postgres")]
public class RegistrationMaxParticipantsTriggerIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RegistrationMaxParticipantsTriggerIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<HorseRacingDataContext> CreateContextAsync(bool reset = true)
    {
        if (reset)
            await _fixture.ResetAsync();
        DbContextOptions<HorseRacingDataContext> options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static (Racecourse racecourse, Account owner, Account jockey1, Account jockey2, Horse horse1, Horse horse2, Race race)
        BuildRaceWithMaxOneSeat()
    {
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = "owner@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey1 = new Account { Id = Guid.NewGuid(), Email = "jockey1@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var jockey2 = new Account { Id = Guid.NewGuid(), Email = "jockey2@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var horse1 = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Horse1", Status = HorseStatus.Healthy };
        var horse2 = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Horse2", Status = HorseStatus.Healthy };
        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = RaceStatus.Scheduled,
            StartTime = DateTimeOffset.UtcNow.AddDays(1),
            MaxParticipants = 1
        };
        return (racecourse, owner, jockey1, jockey2, horse1, horse2, race);
    }

    [Fact]
    public async Task Trigger_RaisesP0001_WhenConfirmingRegistrationWouldExceedMaxParticipants()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        var (racecourse, owner, jockey1, jockey2, horse1, horse2, race) = BuildRaceWithMaxOneSeat();

        var reg1 = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse1.Id, JockeyId = jockey1.Id, Status = RegistrationStatus.Pending };
        var reg2 = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse2.Id, JockeyId = jockey2.Id, Status = RegistrationStatus.Pending };

        db.AddRange(racecourse, owner, jockey1, jockey2, horse1, horse2, race, reg1, reg2);
        await db.SaveChangesAsync();

        reg1.Status = RegistrationStatus.Confirmed;
        await db.SaveChangesAsync();

        reg2.Status = RegistrationStatus.Confirmed;
        DbUpdateException ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        PostgresException pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal("P0001", pg.SqlState);
    }

    [Fact]
    public async Task Trigger_SerializesConcurrentConfirmations_SoOnlyOneWinsTheLastSeat()
    {
        await using HorseRacingDataContext seedDb = await CreateContextAsync();
        var (racecourse, owner, jockey1, jockey2, horse1, horse2, race) = BuildRaceWithMaxOneSeat();

        var reg1 = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse1.Id, JockeyId = jockey1.Id, Status = RegistrationStatus.Pending };
        var reg2 = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse2.Id, JockeyId = jockey2.Id, Status = RegistrationStatus.Pending };

        seedDb.AddRange(racecourse, owner, jockey1, jockey2, horse1, horse2, race, reg1, reg2);
        await seedDb.SaveChangesAsync();

        await using var connA = new NpgsqlConnection(_fixture.ConnectionString);
        await connA.OpenAsync();
        await using NpgsqlTransaction txA = await connA.BeginTransactionAsync();

        await using var connB = new NpgsqlConnection(_fixture.ConnectionString);
        await connB.OpenAsync();
        await using NpgsqlTransaction txB = await connB.BeginTransactionAsync();

        await using var cmdA = new NpgsqlCommand("UPDATE \"Registrations\" SET \"Status\" = 'Confirmed' WHERE \"RegistrationID\" = @id", connA, txA);
        cmdA.Parameters.AddWithValue("id", reg1.RegistrationId);
        await cmdA.ExecuteNonQueryAsync();

        await using var cmdB = new NpgsqlCommand("UPDATE \"Registrations\" SET \"Status\" = 'Confirmed' WHERE \"RegistrationID\" = @id", connB, txB);
        cmdB.Parameters.AddWithValue("id", reg2.RegistrationId);
        Task<int> cmdBTask = cmdB.ExecuteNonQueryAsync();

        await txA.CommitAsync();

        PostgresException pg = await Assert.ThrowsAsync<PostgresException>(() => cmdBTask);
        Assert.Equal("P0001", pg.SqlState);
        await txB.RollbackAsync();

        await using HorseRacingDataContext verifyDb = await CreateContextAsync(reset: false);
        List<Registration> registrations = await verifyDb.Registrations
            .Where(r => r.RaceId == race.RaceId)
            .ToListAsync();

        Assert.Equal(RegistrationStatus.Confirmed, registrations.Single(r => r.RegistrationId == reg1.RegistrationId).Status);
        Assert.Equal(RegistrationStatus.Pending, registrations.Single(r => r.RegistrationId == reg2.RegistrationId).Status);
    }
}
