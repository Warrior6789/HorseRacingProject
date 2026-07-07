using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HorseRacingProject.Tests;

public class UserProfileServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static UserProfileService CreateService(HorseRacingDataContext db, Mock<ICloudinaryService>? cloudinary = null)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new UserProfileService(uow, (cloudinary ?? new Mock<ICloudinaryService>()).Object);
    }

    private static Account NewAccount() => new Account
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@test.com",
        PasswordHash = "x",
        Role = AccountRole.Spectator,
        Status = AccountStatus.Active
    };

    [Fact]
    public async Task CreateUserProfileAsync_AccountExists_CreatesProfileWithZeroBalance()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount();
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        UserProfileService service = CreateService(db);
        var request = new UserProfileCreateRequest { FullName = "Nguyen Van A", Phone = "0900000000" };

        UserProfileResponse result = await service.CreateUserProfileAsync(account.Id, request);

        Assert.Equal(account.Id, result.AccountId);
        Assert.Equal("Nguyen Van A", result.FullName);
        Assert.Equal(0, result.Balance);

        UserProfile saved = await db.UserProfiles.SingleAsync(p => p.AccountId == account.Id);
        Assert.Equal(0, saved.Balance);
    }

    [Fact]
    public async Task CreateUserProfileAsync_AccountDoesNotExist_Throws()
    {
        using HorseRacingDataContext db = CreateContext();
        UserProfileService service = CreateService(db);

        await Assert.ThrowsAsync<Exception>(
            () => service.CreateUserProfileAsync(Guid.NewGuid(), new UserProfileCreateRequest()));
    }

    [Fact]
    public async Task CreateUserProfileAsync_ProfileAlreadyExists_ThrowsInvalidOperationException()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount();
        db.Accounts.Add(account);
        db.UserProfiles.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id, Balance = 0 });
        await db.SaveChangesAsync();

        UserProfileService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateUserProfileAsync(account.Id, new UserProfileCreateRequest()));
    }

    [Fact]
    public async Task CreateUserProfileAsync_WithImage_UploadsAndSetsImageUrl()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount();
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var mockCloudinary = new Mock<ICloudinaryService>();
        mockCloudinary
            .Setup(c => c.UploadImageAsync(It.IsAny<IFormFile>(), "user-profiles"))
            .ReturnsAsync("https://cdn.test/avatar.png");

        UserProfileService service = CreateService(db, mockCloudinary);
        var request = new UserProfileCreateRequest { Image = Mock.Of<IFormFile>() };

        UserProfileResponse result = await service.CreateUserProfileAsync(account.Id, request);

        Assert.Equal("https://cdn.test/avatar.png", result.ImageUrl);
        mockCloudinary.Verify(c => c.UploadImageAsync(It.IsAny<IFormFile>(), "user-profiles"), Times.Once);
    }

    [Fact]
    public async Task GetUserProfileByIdAsync_EmptyAccountId_Throws()
    {
        using HorseRacingDataContext db = CreateContext();
        UserProfileService service = CreateService(db);

        await Assert.ThrowsAsync<Exception>(() => service.GetUserProfileByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task GetUserProfileByIdAsync_NotFound_Throws()
    {
        using HorseRacingDataContext db = CreateContext();
        UserProfileService service = CreateService(db);

        await Assert.ThrowsAsync<Exception>(() => service.GetUserProfileByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetUserProfileByIdAsync_Found_ReturnsMappedData()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount();
        var profile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id, FullName = "Tran Thi B", Balance = 50_000 };
        db.Accounts.Add(account);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();

        UserProfileService service = CreateService(db);

        UserProfileResponse result = await service.GetUserProfileByIdAsync(account.Id);

        Assert.Equal("Tran Thi B", result.FullName);
        Assert.Equal(50_000, result.Balance);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_EmptyAccountId_Throws()
    {
        using HorseRacingDataContext db = CreateContext();
        UserProfileService service = CreateService(db);

        await Assert.ThrowsAsync<Exception>(
            () => service.UpdateUserProfileAsync(Guid.Empty, new UserProfileUpdateRequest()));
    }

    [Fact]
    public async Task UpdateUserProfileAsync_ProfileNotFound_Throws()
    {
        using HorseRacingDataContext db = CreateContext();
        UserProfileService service = CreateService(db);

        await Assert.ThrowsAsync<Exception>(
            () => service.UpdateUserProfileAsync(Guid.NewGuid(), new UserProfileUpdateRequest()));
    }

    [Fact]
    public async Task UpdateUserProfileAsync_OnlyPhoneProvided_KeepsExistingFullName()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount();
        var profile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id, FullName = "Original Name", Phone = "0900000000" };
        db.Accounts.Add(account);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();

        UserProfileService service = CreateService(db);
        var request = new UserProfileUpdateRequest { Phone = "0911111111" };

        await service.UpdateUserProfileAsync(account.Id, request);

        UserProfile updated = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == account.Id);
        Assert.Equal("Original Name", updated.FullName);
        Assert.Equal("0911111111", updated.Phone);
    }

    [Fact]
    public async Task UploadImageAsync_ProfileNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        UserProfileService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.UploadImageAsync(Guid.NewGuid(), Mock.Of<IFormFile>()));
    }

    [Fact]
    public async Task UploadImageAsync_ProfileFound_UpdatesImageUrl()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount();
        var profile = new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id };
        db.Accounts.Add(account);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();

        var mockCloudinary = new Mock<ICloudinaryService>();
        mockCloudinary
            .Setup(c => c.UploadImageAsync(It.IsAny<IFormFile>(), "user-profiles"))
            .ReturnsAsync("https://cdn.test/new.png");

        UserProfileService service = CreateService(db, mockCloudinary);

        string url = await service.UploadImageAsync(account.Id, Mock.Of<IFormFile>());

        Assert.Equal("https://cdn.test/new.png", url);
        UserProfile updated = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.AccountId == account.Id);
        Assert.Equal("https://cdn.test/new.png", updated.ImageUrl);
    }

    [Fact]
    public async Task GetAllUserProfilesPagedAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        for (int i = 0; i < 3; i++)
        {
            Account account = NewAccount();
            db.Accounts.Add(account);
            db.UserProfiles.Add(new UserProfile
            {
                ProfileId = Guid.NewGuid(),
                AccountId = account.Id,
                CreateAt = DateTimeOffset.UtcNow.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        UserProfileService service = CreateService(db);

        PagedResponse<UserProfileResponse> result = await service.GetAllUserProfilesPagedAsync(page: 0, pageSize: 500);

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task GetAllUserProfilesAsync_ReturnsAllProfiles()
    {
        using HorseRacingDataContext db = CreateContext();
        for (int i = 0; i < 2; i++)
        {
            Account account = NewAccount();
            db.Accounts.Add(account);
            db.UserProfiles.Add(new UserProfile { ProfileId = Guid.NewGuid(), AccountId = account.Id, FullName = $"User {i}" });
        }
        await db.SaveChangesAsync();

        UserProfileService service = CreateService(db);

        List<UserProfileResponse> result = await service.GetAllUserProfilesAsync();

        Assert.Equal(2, result.Count);
    }
}
