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

public class RefereeReportServiceTests
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

        var hubContext = new Mock<IHubContext<RaceHub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        return hubContext.Object;
    }

    private static RefereeReportService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        var settlementService = new Mock<IRaceSettlementService>();
        settlementService.Setup(s => s.TrySettleAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        return new RefereeReportService(uow, settlementService.Object, CreateHubContext());
    }

    private class Fixture : IDisposable
    {
        public required HorseRacingDataContext Db;
        public required RefereeReportService Service;
        public required Account Referee;
        public required Race Race;
        public required Registration Registration;
        public required Horse Horse;
        public required Account Owner;

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Fixture> SeedAsync(RaceStatus raceStatus = RaceStatus.Live)
    {
        HorseRacingDataContext db = CreateContext();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var owner = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        var jockey = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var referee = new Account { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Referee, Status = AccountStatus.Active };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = raceStatus, StartTime = DateTimeOffset.UtcNow.AddMinutes(-10), RefereeId = referee.Id, PrizePool = 1_000_000m };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };

        db.AddRange(racecourse, owner, jockey, referee, horse, race, registration);
        db.UserProfiles.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owner.Id, Balance = 100_000 });
        await db.SaveChangesAsync();

        return new Fixture { Db = db, Service = CreateService(db), Referee = referee, Race = race, Registration = registration, Horse = horse, Owner = owner };
    }

    [Fact]
    public async Task CreateReportAsync_RaceNotFound_ThrowsKeyNotFound()
    {
        using Fixture fixture = await SeedAsync();
        var dto = new CreateRefereeReportDto { RaceId = Guid.NewGuid(), RegistrationId = fixture.Registration.RegistrationId, IncidentDescription = "Blocked another horse", PenaltyType = PenaltyType.Warning };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.CreateReportAsync(fixture.Referee.Id, dto));
    }

    [Fact]
    public async Task CreateReportAsync_RaceNotLive_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync(raceStatus: RaceStatus.Scheduled);
        var dto = new CreateRefereeReportDto { RaceId = fixture.Race.RaceId, RegistrationId = fixture.Registration.RegistrationId, IncidentDescription = "Blocked another horse", PenaltyType = PenaltyType.Warning };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateReportAsync(fixture.Referee.Id, dto));
    }

    [Fact]
    public async Task CreateReportAsync_RefereeNotAssigned_ThrowsUnauthorized()
    {
        using Fixture fixture = await SeedAsync();
        var dto = new CreateRefereeReportDto { RaceId = fixture.Race.RaceId, RegistrationId = fixture.Registration.RegistrationId, IncidentDescription = "Blocked another horse", PenaltyType = PenaltyType.Warning };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.CreateReportAsync(Guid.NewGuid(), dto));
    }

    [Fact]
    public async Task CreateReportAsync_DuplicatePendingReport_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync();
        fixture.Db.RefereeReports.Add(new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            IncidentDescription = "Existing report",
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await fixture.Db.SaveChangesAsync();

        var dto = new CreateRefereeReportDto { RaceId = fixture.Race.RaceId, RegistrationId = fixture.Registration.RegistrationId, IncidentDescription = "Another incident", PenaltyType = PenaltyType.Warning };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateReportAsync(fixture.Referee.Id, dto));
    }

    [Fact]
    public async Task CreateReportAsync_Valid_CreatesPendingReport()
    {
        using Fixture fixture = await SeedAsync();
        var dto = new CreateRefereeReportDto { RaceId = fixture.Race.RaceId, RegistrationId = fixture.Registration.RegistrationId, IncidentDescription = "Blocked another horse", PenaltyType = PenaltyType.Warning };

        RefereeReportResponse result = await fixture.Service.CreateReportAsync(fixture.Referee.Id, dto);

        Assert.Equal(RefereeReportStatus.Pending.ToString(), result.Status);
        Assert.Equal(1, await fixture.Db.RefereeReports.CountAsync());
    }

    [Fact]
    public async Task ApproveReportAsync_NotFound_ThrowsKeyNotFound()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.ApproveReportAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ApproveReportAsync_NotPending_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync();
        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            IncidentDescription = "Already approved",
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Approved,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ApproveReportAsync(report.ReportId));
    }

    [Fact]
    public async Task ApproveReportAsync_Warning_ApprovesWithoutBalanceChange()
    {
        using Fixture fixture = await SeedAsync();
        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            IncidentDescription = "Minor incident",
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        RefereeReportResponse result = await fixture.Service.ApproveReportAsync(report.ReportId);

        Assert.Equal(RefereeReportStatus.Approved.ToString(), result.Status);
        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(100_000, ownerProfile.Balance);
    }

    [Fact]
    public async Task ApproveReportAsync_Fine_DeductsFineFromOwnerBalance()
    {
        using Fixture fixture = await SeedAsync();
        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            IncidentDescription = "Interfered with another horse",
            PenaltyType = PenaltyType.Fine,
            FineAmount = 30_000m,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ApproveReportAsync(report.ReportId);

        UserProfile ownerProfile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(70_000, ownerProfile.Balance);

        Prize finePrize = await fixture.Db.Prizes.AsNoTracking().SingleAsync(p => p.RegistrationId == fixture.Registration.RegistrationId);
        Assert.Equal(-30_000m, finePrize.Amount);
    }

    [Fact]
    public async Task ApproveReportAsync_Fine_NeverDropsBalanceBelowZero()
    {
        using Fixture fixture = await SeedAsync();
        UserProfile ownerProfile = await fixture.Db.UserProfiles.SingleAsync(p => p.AccountId == fixture.Owner.Id);
        ownerProfile.Balance = 10_000;
        await fixture.Db.SaveChangesAsync();

        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            IncidentDescription = "Serious interference",
            PenaltyType = PenaltyType.Fine,
            FineAmount = 50_000m,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ApproveReportAsync(report.ReportId);

        UserProfile updated = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owner.Id);
        Assert.Equal(0, updated.Balance);
    }

    [Fact]
    public async Task ApproveReportAsync_Disqualification_PromotesNextPositionAndAwardsPrize()
    {
        using Fixture fixture = await SeedAsync();

        Account jockey2 = new() { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        Account owner2 = new() { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        Horse horse2 = new() { Id = Guid.NewGuid(), OwnerId = owner2.Id, HorseName = "Bolt", Status = HorseStatus.Healthy };
        Registration reg2 = new() { RegistrationId = Guid.NewGuid(), RaceId = fixture.Race.RaceId, HorseId = horse2.Id, JockeyId = jockey2.Id, Status = RegistrationStatus.Confirmed };

        var posConfig = new PositionPrizeConfig { PositionPrizeConfigId = Guid.NewGuid(), Pos1Ratio = 0.6f, Pos2Ratio = 0.4f, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        var jockeyConfig = new JockeyRewardConfig { JockeyRewardConfigId = Guid.NewGuid(), WinCut = 0.1f, PlaceCut = 0.05f, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        fixture.Race.PositionPrizeConfigId = posConfig.PositionPrizeConfigId;
        fixture.Race.JockeyRewardConfigId = jockeyConfig.JockeyRewardConfigId;

        var result1 = new RaceResult { ResultId = Guid.NewGuid(), RegistrationId = fixture.Registration.RegistrationId, FinishPosition = 1, IsDisqualified = false };
        var result2 = new RaceResult { ResultId = Guid.NewGuid(), RegistrationId = reg2.RegistrationId, FinishPosition = 2, IsDisqualified = false };

        fixture.Db.AddRange(jockey2, owner2, horse2, reg2, posConfig, jockeyConfig, result1, result2);
        fixture.Db.UserProfiles.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owner2.Id, Balance = 0 });
        await fixture.Db.SaveChangesAsync();

        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            IncidentDescription = "Dangerous riding causing obstruction",
            PenaltyType = PenaltyType.Disqualification,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ApproveReportAsync(report.ReportId);

        RaceResult updatedResult1 = await fixture.Db.RaceResults.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registration.RegistrationId);
        RaceResult updatedResult2 = await fixture.Db.RaceResults.AsNoTracking().SingleAsync(r => r.RegistrationId == reg2.RegistrationId);
        Assert.True(updatedResult1.IsDisqualified);
        Assert.Equal(1, updatedResult2.FinishPosition);

        UserProfile owner2Profile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == owner2.Id);
        Assert.True(owner2Profile.Balance > 0);
    }

    [Fact]
    public async Task RejectReportAsync_NotFound_ThrowsKeyNotFound()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.RejectReportAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RejectReportAsync_Valid_SetsRejectedStatus()
    {
        using Fixture fixture = await SeedAsync();
        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            IncidentDescription = "Unsubstantiated claim",
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        RefereeReportResponse result = await fixture.Service.RejectReportAsync(report.ReportId);

        Assert.Equal(RefereeReportStatus.Rejected.ToString(), result.Status);
    }

    [Fact]
    public async Task GetReportByIdAsync_NonAdminNotOwningReferee_ThrowsUnauthorized()
    {
        using Fixture fixture = await SeedAsync();
        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            IncidentDescription = "Some incident",
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.Service.GetReportByIdAsync(report.ReportId, Guid.NewGuid(), isAdmin: false));
    }

    [Fact]
    public async Task GetReportByIdAsync_OwningReferee_ReturnsReport()
    {
        using Fixture fixture = await SeedAsync();
        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            IncidentDescription = "Some incident",
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        RefereeReportResponse result = await fixture.Service.GetReportByIdAsync(report.ReportId, fixture.Referee.Id, isAdmin: false);

        Assert.Equal(report.ReportId, result.ReportId);
    }

    [Fact]
    public async Task GetReportsByRaceAsync_FiltersByRaceIdAndCountsByStatus()
    {
        using Fixture fixture = await SeedAsync();
        fixture.Db.RefereeReports.Add(new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        fixture.Db.RefereeReports.Add(new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = Guid.NewGuid(),
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Approved,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await fixture.Db.SaveChangesAsync();

        RefereeReportPagedResponse result = await fixture.Service.GetReportsByRaceAsync(fixture.Race.RaceId, page: 1, pageSize: 10);

        Assert.Single(result.Items);
        Assert.Equal(1, result.PendingCount);
        Assert.Equal(0, result.ApprovedCount);
    }

    [Fact]
    public async Task GetMyReportsPagedAsync_ClampsInvalidPageAndPageSize_FiltersByReferee()
    {
        using Fixture fixture = await SeedAsync();
        Account otherReferee = new() { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.Referee, Status = AccountStatus.Active };
        fixture.Db.Accounts.Add(otherReferee);
        for (int i = 0; i < 3; i++)
        {
            fixture.Db.RefereeReports.Add(new RefereeReport
            {
                ReportId = Guid.NewGuid(),
                RaceId = fixture.Race.RaceId,
                RefereeId = fixture.Referee.Id,
                RegistrationId = fixture.Registration.RegistrationId,
                PenaltyType = PenaltyType.Warning,
                Status = RefereeReportStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i)
            });
        }
        fixture.Db.RefereeReports.Add(new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = otherReferee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await fixture.Db.SaveChangesAsync();

        PagedResponse<RefereeReportResponse> result = await fixture.Service.GetMyReportsPagedAsync(fixture.Referee.Id, page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task UpdateReportAsync_NotFound_ThrowsKeyNotFound()
    {
        using Fixture fixture = await SeedAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.Service.UpdateReportAsync(Guid.NewGuid(), fixture.Referee.Id, new UpdateRefereeReportDto()));
    }

    [Fact]
    public async Task UpdateReportAsync_WrongReferee_ThrowsUnauthorized()
    {
        using Fixture fixture = await SeedAsync();
        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.Service.UpdateReportAsync(report.ReportId, Guid.NewGuid(), new UpdateRefereeReportDto()));
    }

    [Fact]
    public async Task UpdateReportAsync_NotPending_ThrowsInvalidOperation()
    {
        using Fixture fixture = await SeedAsync();
        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Approved,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.UpdateReportAsync(report.ReportId, fixture.Referee.Id, new UpdateRefereeReportDto()));
    }

    [Fact]
    public async Task UpdateReportAsync_Valid_UpdatesIncidentDescription()
    {
        using Fixture fixture = await SeedAsync();
        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registration.RegistrationId,
            IncidentDescription = "Original text",
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.RefereeReports.Add(report);
        await fixture.Db.SaveChangesAsync();

        RefereeReportResponse result = await fixture.Service.UpdateReportAsync(
            report.ReportId, fixture.Referee.Id, new UpdateRefereeReportDto { IncidentDescription = "Updated text" });

        Assert.Equal("Updated text", result.IncidentDescription);
    }
}
