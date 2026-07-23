using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingProject.Tests;

[Collection("Postgres")]
public class DashboardServiceIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public DashboardServiceIntegrationTests(PostgresContainerFixture fixture)
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

    [Fact]
    public async Task GetFinancialAsync_BucketsAllFourTransactionTypesByDay()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = "dashboard-test@example.com",
            PasswordHash = "x",
            Role = AccountRole.Spectator,
            Status = AccountStatus.Active
        };
        db.Add(account);

        var day1 = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        var day2 = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);

        db.AddRange(
            new WalletTransaction { WalletTransactionId = Guid.NewGuid(), AccountId = account.Id, Type = WalletTransactionType.Deposit, Amount = 5_000_000, BalanceAfter = 5_000_000, CreatedAt = day1 },
            new WalletTransaction { WalletTransactionId = Guid.NewGuid(), AccountId = account.Id, Type = WalletTransactionType.Withdrawal, Amount = -2_000_000, BalanceAfter = 3_000_000, CreatedAt = day1 },
            new WalletTransaction { WalletTransactionId = Guid.NewGuid(), AccountId = account.Id, Type = WalletTransactionType.BetPayout, Amount = 1_500_000, BalanceAfter = 4_500_000, CreatedAt = day1 },
            new WalletTransaction { WalletTransactionId = Guid.NewGuid(), AccountId = account.Id, Type = WalletTransactionType.PrizePayout, Amount = 300_000, BalanceAfter = 4_800_000, CreatedAt = day1 },
            new WalletTransaction { WalletTransactionId = Guid.NewGuid(), AccountId = account.Id, Type = WalletTransactionType.Deposit, Amount = 3_200_000, BalanceAfter = 8_000_000, CreatedAt = day2 },
            new WalletTransaction { WalletTransactionId = Guid.NewGuid(), AccountId = account.Id, Type = WalletTransactionType.BetPlaced, Amount = -1_000_000, BalanceAfter = 7_000_000, CreatedAt = day2 }
        );
        await db.SaveChangesAsync();

        var service = new DashboardService(new UnitofWork(db));
        HorseRacingAPI.Dtos.DashboardFinancialResponse result = await service.GetFinancialAsync(null, null, "day");

        Assert.Equal(2, result.TransactionsByPeriod.Count);

        HorseRacingAPI.Dtos.TransactionPeriodPoint point1 = result.TransactionsByPeriod[0];
        Assert.Equal(5_000_000, point1.Deposit);
        Assert.Equal(2_000_000, point1.Withdrawal);
        Assert.Equal(1_500_000, point1.BetPayout);
        Assert.Equal(300_000, point1.PrizePayout);

        HorseRacingAPI.Dtos.TransactionPeriodPoint point2 = result.TransactionsByPeriod[1];
        Assert.Equal(3_200_000, point2.Deposit);
        Assert.Equal(0, point2.Withdrawal);
        Assert.Equal(0, point2.BetPayout);
        Assert.Equal(0, point2.PrizePayout);

        Assert.Equal(2, result.DepositsByPeriod.Count);
        Assert.Equal(5_000_000, result.DepositsByPeriod[0].Amount);
        Assert.Equal(3_200_000, result.DepositsByPeriod[1].Amount);

        Assert.Equal(8_200_000, result.Financial.TotalDeposits);
        Assert.Equal(2_000_000, result.Financial.TotalWithdrawals);
        Assert.Equal(1_500_000, result.Financial.TotalBetPayouts);
        Assert.Equal(300_000, result.Financial.TotalPrizePayouts);
        Assert.Equal(1_000_000, result.Financial.TotalBetsPlaced);
    }

    [Fact]
    public async Task GetRaceStatusBreakdownAsync_GroupsRacesByStatus()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();

        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Test Racecourse" };
        db.Add(racecourse);

        db.AddRange(
            new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled },
            new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled },
            new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Live },
            new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished },
            new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Cancelled }
        );
        await db.SaveChangesAsync();

        var service = new DashboardService(new UnitofWork(db));
        HorseRacingAPI.Dtos.RaceStatusBreakdownResponse result = await service.GetRaceStatusBreakdownAsync();

        Assert.Equal(5, result.TotalRaces);
        Assert.Equal(4, result.ByStatus.Count);
        Assert.Equal(2, result.ByStatus.Single(s => s.Status == nameof(RaceStatus.Scheduled)).Count);
        Assert.Equal(1, result.ByStatus.Single(s => s.Status == nameof(RaceStatus.Live)).Count);
        Assert.Equal(1, result.ByStatus.Single(s => s.Status == nameof(RaceStatus.Finished)).Count);
        Assert.Equal(1, result.ByStatus.Single(s => s.Status == nameof(RaceStatus.Cancelled)).Count);
    }
}
