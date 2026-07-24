using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HorseRacingProject.Tests;

[Collection("Postgres")]
public class RefereeReportServiceIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RefereeReportServiceIntegrationTests(PostgresContainerFixture fixture)
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

    private static IServiceScopeFactory CreateScopeFactory(IUnitofWork uow)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IUnitofWork))).Returns(uow);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private class Fixture : IDisposable
    {
        public required HorseRacingDataContext Db;
        public required RefereeReportService Service;
        public required Race Race;
        public required Account Referee;
        public required Registration[] Registrations;
        public required Account[] Owners;
        public required Account[] Jockeys;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedAsync(RaceStatus raceStatus, bool withRaceResults = false, bool withActiveConfigsLinkedToRace = false, bool withExistingSettledPrizes = false)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var referee = new Account { Id = Guid.NewGuid(), Email = "referee@test.com", PasswordHash = "x", Role = AccountRole.Referee, Status = AccountStatus.Active };

        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RacecourseId = racecourse.Id,
            Status = raceStatus,
            RefereeId = referee.Id,
            PrizePool = 1_000_000m,
            StartTime = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var posConfig = new PositionPrizeConfig
        {
            PositionPrizeConfigId = Guid.NewGuid(),
            Pos1Ratio = 0.5f,
            Pos2Ratio = 0.3f,
            Pos3Ratio = 0.2f,
            Status = ConfigStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var jockeyConfig = new JockeyRewardConfig
        {
            JockeyRewardConfigId = Guid.NewGuid(),
            WinCut = 0.10f,
            PlaceCut = 0.05f,
            Status = ConfigStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AddRange(posConfig, jockeyConfig);

        if (withActiveConfigsLinkedToRace)
        {
            race.PositionPrizeConfigId = posConfig.PositionPrizeConfigId;
            race.JockeyRewardConfigId = jockeyConfig.JockeyRewardConfigId;
        }

        var owners = new Account[3];
        var jockeys = new Account[3];
        var horses = new Horse[3];
        var registrations = new Registration[3];
        var ownerBalances = new long[] { 450_000, 285_000, 200_000 };
        var jockeyBalances = new long[] { 50_000, 15_000, 0 };

        for (int i = 0; i < 3; i++)
        {
            owners[i] = new Account { Id = Guid.NewGuid(), Email = $"owner{i}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
            jockeys[i] = new Account { Id = Guid.NewGuid(), Email = $"jockey{i}@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
            horses[i] = new Horse { Id = Guid.NewGuid(), OwnerId = owners[i].Id, HorseName = $"Horse{i}", Status = HorseStatus.Healthy };
            registrations[i] = new Registration
            {
                RegistrationId = Guid.NewGuid(),
                RaceId = race.RaceId,
                HorseId = horses[i].Id,
                JockeyId = jockeys[i].Id,
                Status = RegistrationStatus.Confirmed
            };
            db.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = owners[i].Id, Balance = withExistingSettledPrizes ? ownerBalances[i] : 0 });
            db.Add(new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = jockeys[i].Id, Balance = withExistingSettledPrizes ? jockeyBalances[i] : 0 });
        }

        db.AddRange(racecourse, referee, race);
        db.AddRange(owners);
        db.AddRange(jockeys);
        db.AddRange(horses);
        db.AddRange(registrations);

        if (withRaceResults)
        {
            for (int i = 0; i < 3; i++)
            {
                db.Add(new RaceResult
                {
                    ResultId = Guid.NewGuid(),
                    RegistrationId = registrations[i].RegistrationId,
                    FinishPosition = i + 1,
                    IsDisqualified = false
                });
            }
        }

        if (withExistingSettledPrizes)
        {
            decimal[] ownerPrizes = { 450_000m, 285_000m, 200_000m };
            decimal[] jockeyPrizes = { 50_000m, 15_000m, 0m };
            for (int i = 0; i < 3; i++)
            {
                db.Add(new Prize { PrizeId = Guid.NewGuid(), RegistrationId = registrations[i].RegistrationId, PrizeType = PrizeType.Owner, Amount = ownerPrizes[i], DistributedAt = DateTimeOffset.UtcNow });
                if (jockeyPrizes[i] != 0)
                    db.Add(new Prize { PrizeId = Guid.NewGuid(), RegistrationId = registrations[i].RegistrationId, PrizeType = PrizeType.Jockey, Amount = jockeyPrizes[i], DistributedAt = DateTimeOffset.UtcNow });
            }
        }

        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var settlementService = new RaceSettlementService(CreateScopeFactory(uow), CreateHubContext());
        var service = new RefereeReportService(uow, settlementService, CreateHubContext());

        return new Fixture { Db = db, Service = service, Race = race, Referee = referee, Registrations = registrations, Owners = owners, Jockeys = jockeys };
    }

    private static CreateRefereeReportDto MakeDto(Guid raceId, Guid registrationId, PenaltyType penaltyType, decimal? fineAmount = null) => new CreateRefereeReportDto
    {
        RaceId = raceId,
        RegistrationId = registrationId,
        IncidentDescription = "Interference in the back stretch",
        PenaltyType = penaltyType,
        FineAmount = fineAmount
    };

    [Fact]
    public async Task CreateReportAsync_ValidData_CreatesPendingReport()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Live);

        RefereeReportResponse response = await fixture.Service.CreateReportAsync(
            fixture.Referee.Id, MakeDto(fixture.Race.RaceId, fixture.Registrations[0].RegistrationId, PenaltyType.Warning));

        Assert.Equal(RefereeReportStatus.Pending.ToString(), response.Status);
    }

    [Fact]
    public async Task CreateReportAsync_RaceNotLive_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateReportAsync(
            fixture.Referee.Id, MakeDto(fixture.Race.RaceId, fixture.Registrations[0].RegistrationId, PenaltyType.Warning)));
    }

    [Fact]
    public async Task CreateReportAsync_NotAssignedReferee_ThrowsUnauthorized()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Live);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.CreateReportAsync(
            Guid.NewGuid(), MakeDto(fixture.Race.RaceId, fixture.Registrations[0].RegistrationId, PenaltyType.Warning)));
    }

    [Fact]
    public async Task CreateReportAsync_RegistrationNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Live);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.CreateReportAsync(
            fixture.Referee.Id, MakeDto(fixture.Race.RaceId, Guid.NewGuid(), PenaltyType.Warning)));
    }

    [Fact]
    public async Task CreateReportAsync_RaceNotFound_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Live);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.CreateReportAsync(
            fixture.Referee.Id, MakeDto(Guid.NewGuid(), fixture.Registrations[0].RegistrationId, PenaltyType.Warning)));
    }

    [Fact]
    public async Task CreateReportAsync_DuplicateReportForSameHorse_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Live);

        await fixture.Service.CreateReportAsync(fixture.Referee.Id, MakeDto(fixture.Race.RaceId, fixture.Registrations[0].RegistrationId, PenaltyType.Warning));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateReportAsync(
            fixture.Referee.Id, MakeDto(fixture.Race.RaceId, fixture.Registrations[0].RegistrationId, PenaltyType.Fine, 10_000m)));
    }

    [Fact]
    public async Task ApproveReportAsync_LastPendingWarningReport_TriggersSettlementAndDistributesPrizes()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished, withRaceResults: true);

        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registrations[2].RegistrationId,
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.Add(report);
        await fixture.Db.SaveChangesAsync();

        RefereeReportResponse response = await fixture.Service.ApproveReportAsync(report.ReportId);

        Assert.Equal(RefereeReportStatus.Approved.ToString(), response.Status);

        UserProfile owner1 = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owners[0].Id);
        Assert.Equal(450_000L, owner1.Balance);

        bool anyPrizes = await fixture.Db.Prizes.AsNoTracking().AnyAsync(p => p.Registration.RaceId == fixture.Race.RaceId);
        Assert.True(anyPrizes);
    }

    [Fact]
    public async Task ApproveReportAsync_FinePenalty_DeductsOwnerBalanceAndRecordsNegativePrize()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished, withRaceResults: true, withExistingSettledPrizes: true);

        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registrations[0].RegistrationId,
            PenaltyType = PenaltyType.Fine,
            FineAmount = 100_000m,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.Add(report);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ApproveReportAsync(report.ReportId);

        UserProfile owner1 = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owners[0].Id);
        Assert.Equal(350_000L, owner1.Balance);

        Prize finePrize = await fixture.Db.Prizes.AsNoTracking()
            .SingleAsync(p => p.RegistrationId == fixture.Registrations[0].RegistrationId && p.Amount == -100_000m);
        Assert.Equal(PrizeType.Owner, finePrize.PrizeType);
    }

    [Fact]
    public async Task ApproveReportAsync_DisqualificationOfFirstPlace_PromotesLowerPositionsAndReversesPrizes()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished, withRaceResults: true, withActiveConfigsLinkedToRace: true, withExistingSettledPrizes: true);

        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registrations[0].RegistrationId,
            PenaltyType = PenaltyType.Disqualification,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.Add(report);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ApproveReportAsync(report.ReportId);

        RaceResult reg1Result = await fixture.Db.RaceResults.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registrations[0].RegistrationId);
        Assert.True(reg1Result.IsDisqualified);

        RaceResult reg2Result = await fixture.Db.RaceResults.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registrations[1].RegistrationId);
        RaceResult reg3Result = await fixture.Db.RaceResults.AsNoTracking().SingleAsync(r => r.RegistrationId == fixture.Registrations[2].RegistrationId);
        Assert.Equal(1, reg2Result.FinishPosition);
        Assert.Equal(2, reg3Result.FinishPosition);

        UserProfile owner1 = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owners[0].Id);
        JockeyProfile jockey1 = await fixture.Db.JockeyProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Jockeys[0].Id);
        Assert.Equal(0L, owner1.Balance);
        Assert.Equal(0L, jockey1.Balance);

        UserProfile owner2 = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owners[1].Id);
        JockeyProfile jockey2 = await fixture.Db.JockeyProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Jockeys[1].Id);
        Assert.Equal(450_000L, owner2.Balance);
        Assert.Equal(50_000L, jockey2.Balance);

        UserProfile owner3 = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Owners[2].Id);
        JockeyProfile jockey3 = await fixture.Db.JockeyProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Jockeys[2].Id);
        Assert.Equal(285_000L, owner3.Balance);
        Assert.Equal(15_000L, jockey3.Balance);
    }

    [Fact]
    public async Task ApproveReportAsync_NotPending_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished, withRaceResults: true);

        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registrations[0].RegistrationId,
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Approved,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.Add(report);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ApproveReportAsync(report.ReportId));
    }

    [Fact]
    public async Task ApproveReportAsync_NotFound_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Live);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.ApproveReportAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RejectReportAsync_Pending_SetsRejectedAndTriggersSettlement()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished, withRaceResults: true);

        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registrations[0].RegistrationId,
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.Add(report);
        await fixture.Db.SaveChangesAsync();

        RefereeReportResponse response = await fixture.Service.RejectReportAsync(report.ReportId);

        Assert.Equal(RefereeReportStatus.Rejected.ToString(), response.Status);

        bool anyPrizes = await fixture.Db.Prizes.AsNoTracking().AnyAsync(p => p.Registration.RaceId == fixture.Race.RaceId);
        Assert.True(anyPrizes);
    }

    [Fact]
    public async Task RejectReportAsync_NotPending_Throws()
    {
        using Fixture fixture = await SeedAsync(RaceStatus.Finished, withRaceResults: true);

        var report = new RefereeReport
        {
            ReportId = Guid.NewGuid(),
            RaceId = fixture.Race.RaceId,
            RefereeId = fixture.Referee.Id,
            RegistrationId = fixture.Registrations[0].RegistrationId,
            PenaltyType = PenaltyType.Warning,
            Status = RefereeReportStatus.Rejected,
            CreatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.Add(report);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RejectReportAsync(report.ReportId));
    }
}
