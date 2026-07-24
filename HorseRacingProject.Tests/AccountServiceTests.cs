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

public class AccountServiceTests
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

    private static AccountService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new AccountService(uow, CreateHubContext(), CreateRegistrationServiceMock(), CreateRaceRefereeServiceMock());
    }

    private static IRegistrationService CreateRegistrationServiceMock()
    {
        var mock = new Mock<IRegistrationService>();
        mock.Setup(s => s.ScratchHorseAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        mock.Setup(s => s.AdminRejectRegistrationAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        return mock.Object;
    }

    private static IRaceRefereeService CreateRaceRefereeServiceMock()
    {
        var mock = new Mock<IRaceRefereeService>();
        mock.Setup(s => s.UnassignAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        return mock.Object;
    }

    private static Account NewAccount(AccountRole role, AccountStatus status) => new Account
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@test.com",
        PasswordHash = "x",
        Role = role,
        Status = status,
        CreateAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task RestoreAccountAsync_NotFound_ThrowsArgumentException()
    {
        using HorseRacingDataContext db = CreateContext();
        AccountService service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RestoreAccountAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RestoreAccountAsync_NotSuspended_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator, AccountStatus.Active);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreAccountAsync(account.Id));
    }

    [Fact]
    public async Task RestoreAccountAsync_Valid_SetsStatusActive()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator, AccountStatus.Suspended);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        await service.RestoreAccountAsync(account.Id);

        Account updated = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Equal(AccountStatus.Active, updated.Status);
    }

    [Fact]
    public async Task SuspendAccountAsync_NotFound_ThrowsArgumentException()
    {
        using HorseRacingDataContext db = CreateContext();
        AccountService service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SuspendAccountAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SuspendAccountAsync_NotActive_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator, AccountStatus.Suspended);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SuspendAccountAsync(account.Id));
    }

    [Fact]
    public async Task SuspendAccountAsync_Valid_SetsStatusSuspended()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator, AccountStatus.Active);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        await service.SuspendAccountAsync(account.Id);

        Account updated = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Equal(AccountStatus.Suspended, updated.Status);
    }

    [Fact]
    public async Task GetAccountByStatusPagedAsync_FiltersByRoleAndSearch()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner, AccountStatus.Active);
        owner.Email = "owner-match@test.com";
        Account jockey = NewAccount(AccountRole.Jockey, AccountStatus.Active);
        jockey.Email = "jockey-nomatch@test.com";
        db.Accounts.AddRange(owner, jockey);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        PagedResponse<AccountResponse> result = await service.GetAccountByStatusPagedAsync(
            "Active", page: 1, pageSize: 10, role: "HorseOwner", search: "match");

        Assert.Single(result.Items);
        Assert.Equal(owner.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task GetRoleUpgradeRequestsPagedAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        for (int i = 0; i < 3; i++)
        {
            Account account = NewAccount(AccountRole.Spectator, AccountStatus.Active);
            account.RequestedRole = AccountRole.HorseOwner;
            db.Accounts.Add(account);
        }
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        PagedResponse<UpgradeRequestResponse> result = await service.GetRoleUpgradeRequestsPagedAsync(page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetUpgradeRequestDetailAsync_NoPendingRequest_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator, AccountStatus.Active);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetUpgradeRequestDetailAsync(account.Id));
    }

    [Fact]
    public async Task GetUpgradeRequestDetailAsync_ReturnsDetail()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator, AccountStatus.Active);
        account.RequestedRole = AccountRole.HorseOwner;
        var userProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id, FullName = "Owner Le", Phone = "0900000000" };
        db.Accounts.Add(account);
        db.UserProfiles.Add(userProfile);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        UpgradeRequestResponse result = await service.GetUpgradeRequestDetailAsync(account.Id);

        Assert.Equal("Owner Le", result.FullName);
        Assert.Equal("HorseOwner", result.RequestedRole);
    }

    [Fact]
    public async Task ApproveRoleUpgradeAsync_AccountNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        AccountService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ApproveRoleUpgradeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ApproveRoleUpgradeAsync_NoPendingRequest_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator, AccountStatus.Active);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveRoleUpgradeAsync(account.Id));
    }

    [Fact]
    public async Task ApproveRoleUpgradeAsync_JockeyRequest_MergesBalanceAndRemovesUserProfile()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator, AccountStatus.Active);
        account.RequestedRole = AccountRole.Jockey;
        var userProfile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id, FullName = "Spectator A", Balance = 50_000, ImageUrl = "https://cdn.test/avatar.jpg" };
        var jockeyProfile = new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = account.Id, FullName = "Spectator A", LicenseNumber = "LIC-002" };
        db.Accounts.Add(account);
        db.UserProfiles.Add(userProfile);
        db.JockeyProfiles.Add(jockeyProfile);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        await service.ApproveRoleUpgradeAsync(account.Id);

        Account updatedAccount = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        JockeyProfile updatedJockey = await db.JockeyProfiles.AsNoTracking().SingleAsync(j => j.AccountId == account.Id);
        Assert.Equal(AccountRole.Jockey, updatedAccount.Role);
        Assert.Null(updatedAccount.RequestedRole);
        Assert.Equal(50_000, updatedJockey.Balance);
        Assert.Equal("https://cdn.test/avatar.jpg", updatedJockey.ImageUrl);
        Assert.False(await db.UserProfiles.AnyAsync(u => u.AccountId == account.Id));
    }

    [Fact]
    public async Task RejectRoleUpgradeAsync_AccountNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        AccountService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RejectRoleUpgradeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RejectRoleUpgradeAsync_NoPendingRequest_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator, AccountStatus.Active);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectRoleUpgradeAsync(account.Id));
    }

    [Fact]
    public async Task RejectRoleUpgradeAsync_JockeyRequest_RemovesJockeyProfileAndClearsRequest()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator, AccountStatus.Active);
        account.RequestedRole = AccountRole.Jockey;
        var jockeyProfile = new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = account.Id, FullName = "Spectator A", LicenseNumber = "LIC-003" };
        db.Accounts.Add(account);
        db.JockeyProfiles.Add(jockeyProfile);
        await db.SaveChangesAsync();
        AccountService service = CreateService(db);

        await service.RejectRoleUpgradeAsync(account.Id);

        Account updatedAccount = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Null(updatedAccount.RequestedRole);
        Assert.False(await db.JockeyProfiles.AnyAsync(j => j.AccountId == account.Id));
    }
}
