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
public class WithdrawalServiceIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public WithdrawalServiceIntegrationTests(PostgresContainerFixture fixture)
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
        public required WithdrawalService Service;
        public required Account Account;
        public required Withdrawal Withdrawal;

        public void Dispose() => Db.Dispose();
    }

    private async Task<Fixture> SeedSpectatorAsync(long balance, long withdrawalAmount, WithdrawalStatus status = WithdrawalStatus.Pending)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var account = new Account { Id = Guid.NewGuid(), Email = "spectator@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        var profile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id, Balance = balance };
        var withdrawal = new Withdrawal
        {
            WithdrawalId = Guid.NewGuid(),
            AccountId = account.Id,
            Amount = withdrawalAmount,
            BankAccountNumber = "0123456789",
            BankName = "Test Bank",
            AccountHolderName = "Spectator Name",
            Status = status,
            CreateAt = DateTimeOffset.UtcNow
        };

        db.AddRange(account, profile, withdrawal);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new WithdrawalService(uow, CreateHubContext());

        return new Fixture { Db = db, Service = service, Account = account, Withdrawal = withdrawal };
    }

    private async Task<Fixture> SeedJockeyAsync(long balance, long withdrawalAmount, WithdrawalStatus status = WithdrawalStatus.Pending)
    {
        HorseRacingDataContext db = await CreateContextAsync();

        var account = new Account { Id = Guid.NewGuid(), Email = "jockey@test.com", PasswordHash = "x", Role = AccountRole.Jockey, Status = AccountStatus.Active };
        var profile = new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = account.Id, Balance = balance };
        var withdrawal = new Withdrawal
        {
            WithdrawalId = Guid.NewGuid(),
            AccountId = account.Id,
            Amount = withdrawalAmount,
            BankAccountNumber = "0123456789",
            BankName = "Test Bank",
            AccountHolderName = "Jockey Name",
            Status = status,
            CreateAt = DateTimeOffset.UtcNow
        };

        db.AddRange(account, profile, withdrawal);
        await db.SaveChangesAsync();

        IUnitofWork uow = new UnitofWork(db);
        var service = new WithdrawalService(uow, CreateHubContext());

        return new Fixture { Db = db, Service = service, Account = account, Withdrawal = withdrawal };
    }

    [Fact]
    public async Task ApproveAsync_SpectatorWithSufficientBalance_DeductsAndCompletes()
    {
        using Fixture fixture = await SeedSpectatorAsync(balance: 10_000, withdrawalAmount: 4_000);

        WithdrawalResponse response = await fixture.Service.ApproveAsync(fixture.Withdrawal.WithdrawalId, new ProcessWithdrawalDto { AdminNote = "ok" });

        Assert.Equal(WithdrawalStatus.Completed.ToString(), response.Status);

        UserProfile profile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Account.Id);
        Assert.Equal(6_000L, profile.Balance);
    }

    [Fact]
    public async Task ApproveAsync_JockeyWithSufficientBalance_DeductsAndCompletes()
    {
        using Fixture fixture = await SeedJockeyAsync(balance: 5_000, withdrawalAmount: 2_000);

        WithdrawalResponse response = await fixture.Service.ApproveAsync(fixture.Withdrawal.WithdrawalId, new ProcessWithdrawalDto());

        Assert.Equal(WithdrawalStatus.Completed.ToString(), response.Status);

        JockeyProfile profile = await fixture.Db.JockeyProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Account.Id);
        Assert.Equal(3_000L, profile.Balance);
    }

    [Fact]
    public async Task ApproveAsync_BalanceDroppedBelowAmountSinceRequest_AutoRejectsAndLeavesBalanceUnchanged()
    {
        using Fixture fixture = await SeedSpectatorAsync(balance: 1_000, withdrawalAmount: 4_000);

        WithdrawalResponse response = await fixture.Service.ApproveAsync(fixture.Withdrawal.WithdrawalId, new ProcessWithdrawalDto());

        Assert.Equal(WithdrawalStatus.Rejected.ToString(), response.Status);
        Assert.Contains("insufficient balance", response.AdminNote, StringComparison.OrdinalIgnoreCase);

        UserProfile profile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Account.Id);
        Assert.Equal(1_000L, profile.Balance);
    }

    [Fact]
    public async Task ApproveAsync_NotPending_Throws()
    {
        using Fixture fixture = await SeedSpectatorAsync(balance: 10_000, withdrawalAmount: 4_000, status: WithdrawalStatus.Completed);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.ApproveAsync(fixture.Withdrawal.WithdrawalId, new ProcessWithdrawalDto()));
    }

    [Fact]
    public async Task ApproveAsync_NotFound_Throws()
    {
        using Fixture fixture = await SeedSpectatorAsync(balance: 10_000, withdrawalAmount: 4_000);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.Service.ApproveAsync(Guid.NewGuid(), new ProcessWithdrawalDto()));
    }

    [Fact]
    public async Task RejectAsync_Pending_MarksRejectedAndLeavesBalanceUnchanged()
    {
        using Fixture fixture = await SeedSpectatorAsync(balance: 10_000, withdrawalAmount: 4_000);

        WithdrawalResponse response = await fixture.Service.RejectAsync(fixture.Withdrawal.WithdrawalId, new ProcessWithdrawalDto { AdminNote = "not eligible" });

        Assert.Equal(WithdrawalStatus.Rejected.ToString(), response.Status);
        Assert.Equal("not eligible", response.AdminNote);

        UserProfile profile = await fixture.Db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == fixture.Account.Id);
        Assert.Equal(10_000L, profile.Balance);
    }

    [Fact]
    public async Task RejectAsync_NotPending_Throws()
    {
        using Fixture fixture = await SeedSpectatorAsync(balance: 10_000, withdrawalAmount: 4_000, status: WithdrawalStatus.Rejected);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.RejectAsync(fixture.Withdrawal.WithdrawalId, new ProcessWithdrawalDto()));
    }
}
