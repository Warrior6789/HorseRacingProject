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

}
