using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace HorseRacingProject.Tests;

public class AuthServiceTests
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

    private static IConfiguration CreateConfig()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-super-secret-signing-key-1234567890",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:DurationInMinutes"] = "180"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
    }

    private static AuthService CreateService(HorseRacingDataContext db, Mock<ICloudinaryService>? cloudinary = null)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new AuthService(uow, CreateConfig(), (cloudinary ?? new Mock<ICloudinaryService>()).Object, CreateHubContext());
    }

    private static RegisterDto NewRegisterDto(string email, string password = "P@ssw0rd") => new RegisterDto
    {
        Email = email,
        Password = password,
        FullName = "Nguyen Van A",
        Phone = "0900000000"
    };

    [Fact]
    public async Task RegisterAsync_EmailExists_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        db.Accounts.Add(new Account { Id = Guid.NewGuid(), Email = "existing@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active });
        await db.SaveChangesAsync();
        AuthService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterAsync(NewRegisterDto("existing@test.com")));
    }

    [Fact]
    public async Task RegisterAsync_MissingFullName_ThrowsArgumentException()
    {
        using HorseRacingDataContext db = CreateContext();
        AuthService service = CreateService(db);
        var dto = NewRegisterDto("new@test.com");
        dto.FullName = " ";

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_MissingPhone_ThrowsArgumentException()
    {
        using HorseRacingDataContext db = CreateContext();
        AuthService service = CreateService(db);
        var dto = NewRegisterDto("new@test.com");
        dto.Phone = "";

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_Valid_CreatesSpectatorAccountWithZeroBalanceProfile()
    {
        using HorseRacingDataContext db = CreateContext();
        AuthService service = CreateService(db);

        await service.RegisterAsync(NewRegisterDto("new@test.com"));

        Account account = await db.Accounts.AsNoTracking().SingleAsync(a => a.Email == "new@test.com");
        UserProfile profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == account.Id);
        Assert.Equal(AccountRole.Spectator, account.Role);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal("Nguyen Van A", profile.FullName);
        Assert.Equal(0, profile.Balance);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsNull()
    {
        using HorseRacingDataContext db = CreateContext();
        AuthService service = CreateService(db);

        string? token = await service.LoginAsync(new LoginDto { Email = "nobody@test.com", Password = "x" });

        Assert.Null(token);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        using HorseRacingDataContext db = CreateContext();
        AuthService service = CreateService(db);
        await service.RegisterAsync(NewRegisterDto("login1@test.com", "CorrectPass1"));

        string? token = await service.LoginAsync(new LoginDto { Email = "login1@test.com", Password = "WrongPass" });

        Assert.Null(token);
    }

    [Fact]
    public async Task LoginAsync_BannedAccount_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        AuthService service = CreateService(db);
        await service.RegisterAsync(NewRegisterDto("login2@test.com", "CorrectPass1"));
        Account account = await db.Accounts.SingleAsync(a => a.Email == "login2@test.com");
        account.Status = AccountStatus.Banned;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LoginAsync(new LoginDto { Email = "login2@test.com", Password = "CorrectPass1" }));
    }

    [Fact]
    public async Task LoginAsync_SuspendedAccount_ReturnsNull()
    {
        using HorseRacingDataContext db = CreateContext();
        AuthService service = CreateService(db);
        await service.RegisterAsync(NewRegisterDto("login3@test.com", "CorrectPass1"));
        Account account = await db.Accounts.SingleAsync(a => a.Email == "login3@test.com");
        account.Status = AccountStatus.Suspended;
        await db.SaveChangesAsync();

        string? token = await service.LoginAsync(new LoginDto { Email = "login3@test.com", Password = "CorrectPass1" });

        Assert.Null(token);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        using HorseRacingDataContext db = CreateContext();
        AuthService service = CreateService(db);
        await service.RegisterAsync(NewRegisterDto("login4@test.com", "CorrectPass1"));

        string? token = await service.LoginAsync(new LoginDto { Email = "login4@test.com", Password = "CorrectPass1" });

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task RequestRoleUpgradeAsync_AlreadyPending_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = new Account { Id = Guid.NewGuid(), Email = "pending@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active, RequestedRole = AccountRole.HorseOwner };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AuthService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RequestRoleUpgradeAsync(account.Id, new RequestRoleUpgradeDto { RequestedRole = "Jockey", LicenseNumber = "LIC-1" }));
    }

    [Fact]
    public async Task RequestRoleUpgradeAsync_NotSpectator_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = new Account { Id = Guid.NewGuid(), Email = "owner@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AuthService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RequestRoleUpgradeAsync(account.Id, new RequestRoleUpgradeDto { RequestedRole = "Jockey", LicenseNumber = "LIC-1" }));
    }

    [Fact]
    public async Task RequestRoleUpgradeAsync_JockeyMissingLicenseNumber_ThrowsArgumentException()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = new Account { Id = Guid.NewGuid(), Email = "spectator1@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AuthService service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RequestRoleUpgradeAsync(account.Id, new RequestRoleUpgradeDto { RequestedRole = "Jockey" }));
    }

    [Fact]
    public async Task RequestRoleUpgradeAsync_ValidJockeyRequest_CreatesJockeyProfileAndSetsRequestedRole()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = new Account { Id = Guid.NewGuid(), Email = "spectator2@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AuthService service = CreateService(db);

        await service.RequestRoleUpgradeAsync(account.Id, new RequestRoleUpgradeDto { RequestedRole = "Jockey", FullName = "Jockey X", LicenseNumber = "LIC-9" });

        Account updated = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Equal(AccountRole.Jockey, updated.RequestedRole);
        Assert.True(await db.JockeyProfiles.AnyAsync(j => j.AccountId == account.Id && j.LicenseNumber == "LIC-9"));
    }

    [Fact]
    public async Task RequestRoleUpgradeAsync_HorseOwnerWithoutUserProfile_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = new Account { Id = Guid.NewGuid(), Email = "spectator3@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AuthService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RequestRoleUpgradeAsync(account.Id, new RequestRoleUpgradeDto { RequestedRole = "HorseOwner" }));
    }

    [Fact]
    public async Task RequestRoleUpgradeAsync_ValidHorseOwnerRequest_UpdatesUserProfileAndSetsRequestedRole()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = new Account { Id = Guid.NewGuid(), Email = "spectator4@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active };
        var profile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id, FullName = "Old Name" };
        db.Accounts.Add(account);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        AuthService service = CreateService(db);

        await service.RequestRoleUpgradeAsync(account.Id, new RequestRoleUpgradeDto { RequestedRole = "HorseOwner", FullName = "New Name" });

        Account updated = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        UserProfile updatedProfile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == account.Id);
        Assert.Equal(AccountRole.HorseOwner, updated.RequestedRole);
        Assert.Equal("New Name", updatedProfile.FullName);
    }

    [Fact]
    public async Task GetMeAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        AuthService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetMeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetMeAsync_ReturnsAccountInfo()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = new Account { Id = Guid.NewGuid(), Email = "me@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active, RequestedRole = AccountRole.Jockey };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        AuthService service = CreateService(db);

        MeResponse result = await service.GetMeAsync(account.Id);

        Assert.Equal("me@test.com", result.Email);
        Assert.Equal("Spectator", result.Role);
        Assert.Equal("Jockey", result.RequestedRole);
    }
}
