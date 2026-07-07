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

public class WithdrawalServiceTests
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

    private static WithdrawalService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new WithdrawalService(uow, CreateHubContext());
    }

    private static Account NewAccount(AccountRole role) => new Account
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@test.com",
        PasswordHash = "x",
        Role = role,
        Status = AccountStatus.Active,
        CreateAt = DateTimeOffset.UtcNow
    };

    private static WithdrawalRequest NewRequest(long amount) => new WithdrawalRequest
    {
        Amount = amount,
        BankAccountNumber = "0123456789",
        BankName = "Test Bank",
        AccountHolderName = "Test Holder"
    };

    [Fact]
    public async Task CreateWithdrawalAsync_NonPositiveAmount_ThrowsArgumentException()
    {
        using HorseRacingDataContext db = CreateContext();
        WithdrawalService service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateWithdrawalAsync(Guid.NewGuid(), NewRequest(0)));
    }

    [Fact]
    public async Task CreateWithdrawalAsync_UserProfileNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        WithdrawalService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateWithdrawalAsync(account.Id, NewRequest(1000)));
    }

    [Fact]
    public async Task CreateWithdrawalAsync_JockeyProfileNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Jockey);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        WithdrawalService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateWithdrawalAsync(account.Id, NewRequest(1000)));
    }

    [Fact]
    public async Task CreateWithdrawalAsync_InsufficientBalance_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator);
        var profile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id, Balance = 500 };
        db.Accounts.Add(account);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        WithdrawalService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateWithdrawalAsync(account.Id, NewRequest(1000)));
    }

    [Fact]
    public async Task CreateWithdrawalAsync_Valid_CreatesPendingWithdrawal()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator);
        var profile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id, Balance = 5000 };
        db.Accounts.Add(account);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        WithdrawalService service = CreateService(db);

        WithdrawalResponse result = await service.CreateWithdrawalAsync(account.Id, NewRequest(1000));

        Assert.Equal(WithdrawalStatus.Pending.ToString(), result.Status);
        Assert.Equal(1000, result.Amount);
    }

    [Fact]
    public async Task GetMyHistoryAsync_FiltersByAccountId()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator);
        Account other = NewAccount(AccountRole.Spectator);
        db.Withdrawals.Add(new Withdrawal { WithdrawalId = Guid.NewGuid(), AccountId = account.Id, Amount = 1000, BankAccountNumber = "1", BankName = "B", AccountHolderName = "H", Status = WithdrawalStatus.Pending, CreateAt = DateTimeOffset.UtcNow });
        db.Withdrawals.Add(new Withdrawal { WithdrawalId = Guid.NewGuid(), AccountId = other.Id, Amount = 2000, BankAccountNumber = "2", BankName = "B", AccountHolderName = "H", Status = WithdrawalStatus.Pending, CreateAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        WithdrawalService service = CreateService(db);

        PagedResponse<WithdrawalResponse> result = await service.GetMyHistoryAsync(account.Id, page: 1, pageSize: 10);

        Assert.Single(result.Items);
        Assert.Equal(1000, result.Items[0].Amount);
    }

    [Fact]
    public async Task GetPendingAsync_OnlyReturnsPendingWithdrawals()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator);
        db.Withdrawals.Add(new Withdrawal { WithdrawalId = Guid.NewGuid(), AccountId = account.Id, Amount = 1000, BankAccountNumber = "1", BankName = "B", AccountHolderName = "H", Status = WithdrawalStatus.Pending, CreateAt = DateTimeOffset.UtcNow });
        db.Withdrawals.Add(new Withdrawal { WithdrawalId = Guid.NewGuid(), AccountId = account.Id, Amount = 2000, BankAccountNumber = "1", BankName = "B", AccountHolderName = "H", Status = WithdrawalStatus.Completed, CreateAt = DateTimeOffset.UtcNow, ProcessedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        WithdrawalService service = CreateService(db);

        PagedResponse<WithdrawalResponse> result = await service.GetPendingAsync(page: 1, pageSize: 10);

        Assert.Single(result.Items);
        Assert.Equal(1000, result.Items[0].Amount);
    }

    [Fact]
    public async Task ApproveAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        WithdrawalService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ApproveAsync(Guid.NewGuid(), new ProcessWithdrawalDto()));
    }

    [Fact]
    public async Task ApproveAsync_NotPending_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator);
        var withdrawal = new Withdrawal { WithdrawalId = Guid.NewGuid(), AccountId = account.Id, Amount = 1000, BankAccountNumber = "1", BankName = "B", AccountHolderName = "H", Status = WithdrawalStatus.Rejected, CreateAt = DateTimeOffset.UtcNow };
        db.Accounts.Add(account);
        db.Withdrawals.Add(withdrawal);
        await db.SaveChangesAsync();
        WithdrawalService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApproveAsync(withdrawal.WithdrawalId, new ProcessWithdrawalDto()));
    }

    [Fact]
    public async Task RejectAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        WithdrawalService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.RejectAsync(Guid.NewGuid(), new ProcessWithdrawalDto()));
    }

    [Fact]
    public async Task RejectAsync_NotPending_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator);
        var withdrawal = new Withdrawal { WithdrawalId = Guid.NewGuid(), AccountId = account.Id, Amount = 1000, BankAccountNumber = "1", BankName = "B", AccountHolderName = "H", Status = WithdrawalStatus.Completed, CreateAt = DateTimeOffset.UtcNow, ProcessedAt = DateTimeOffset.UtcNow };
        db.Accounts.Add(account);
        db.Withdrawals.Add(withdrawal);
        await db.SaveChangesAsync();
        WithdrawalService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RejectAsync(withdrawal.WithdrawalId, new ProcessWithdrawalDto()));
    }

    [Fact]
    public async Task RejectAsync_Valid_SetsStatusRejectedWithAdminNote()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator);
        var withdrawal = new Withdrawal { WithdrawalId = Guid.NewGuid(), AccountId = account.Id, Amount = 1000, BankAccountNumber = "1", BankName = "B", AccountHolderName = "H", Status = WithdrawalStatus.Pending, CreateAt = DateTimeOffset.UtcNow };
        db.Accounts.Add(account);
        db.Withdrawals.Add(withdrawal);
        await db.SaveChangesAsync();
        WithdrawalService service = CreateService(db);

        WithdrawalResponse result = await service.RejectAsync(withdrawal.WithdrawalId, new ProcessWithdrawalDto { AdminNote = "Invalid bank info" });

        Assert.Equal(WithdrawalStatus.Rejected.ToString(), result.Status);
        Assert.Equal("Invalid bank info", result.AdminNote);
    }
}
