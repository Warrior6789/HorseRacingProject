using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HorseRacingProject.Tests;

public class JockeyProfileServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static JockeyProfileService CreateService(HorseRacingDataContext db, Mock<ICloudinaryService>? cloudinary = null)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new JockeyProfileService(uow, (cloudinary ?? new Mock<ICloudinaryService>()).Object);
    }

    private static Account NewAccount(AccountRole role) => new Account
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@test.com",
        PasswordHash = "x",
        Role = role,
        Status = AccountStatus.Active
    };

    [Fact]
    public async Task CreateJockeyProfileAsync_AccountDoesNotExist_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyProfileService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateJockeyProfileAsync(Guid.NewGuid(), new JockeyProfileCreateRequest()));
    }

    [Fact]
    public async Task CreateJockeyProfileAsync_AccountNotJockeyRole_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        JockeyProfileService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateJockeyProfileAsync(account.Id, new JockeyProfileCreateRequest()));
    }

    [Fact]
    public async Task CreateJockeyProfileAsync_ProfileAlreadyExists_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Jockey);
        db.Accounts.Add(account);
        db.JockeyProfiles.Add(new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = account.Id });
        await db.SaveChangesAsync();

        JockeyProfileService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateJockeyProfileAsync(account.Id, new JockeyProfileCreateRequest()));
    }

    [Fact]
    public async Task CreateJockeyProfileAsync_Valid_CreatesProfileWithZeroStats()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Jockey);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        JockeyProfileService service = CreateService(db);
        var request = new JockeyProfileCreateRequest { FullName = "Jockey A", Nationality = "VN" };

        JockeyProfileResponse result = await service.CreateJockeyProfileAsync(account.Id, request);

        Assert.Equal("Jockey A", result.FullName);
        Assert.Equal(0, result.TotalRaces);
        Assert.Equal(0, result.TotalWins);
    }

    [Fact]
    public async Task GetJockeyProfileByAccountIdAsync_EmptyAccountId_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyProfileService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetJockeyProfileByAccountIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task GetJockeyProfileByAccountIdAsync_NotFound_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyProfileService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetJockeyProfileByAccountIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetJockeyProfileByAccountIdAsync_Found_ReturnsData()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Jockey);
        var profile = new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = account.Id, FullName = "Jockey B" };
        db.Accounts.Add(account);
        db.JockeyProfiles.Add(profile);
        await db.SaveChangesAsync();

        JockeyProfileService service = CreateService(db);

        JockeyProfileResponse result = await service.GetJockeyProfileByAccountIdAsync(account.Id);

        Assert.Equal("Jockey B", result.FullName);
    }

    [Fact]
    public async Task UpdateJockeyProfileAsync_EmptyAccountId_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyProfileService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateJockeyProfileAsync(Guid.Empty, new JockeyProfileUpdateRequest()));
    }

    [Fact]
    public async Task UpdateJockeyProfileAsync_NotFound_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyProfileService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateJockeyProfileAsync(Guid.NewGuid(), new JockeyProfileUpdateRequest()));
    }

    [Fact]
    public async Task UpdateJockeyProfileAsync_PartialUpdate_KeepsOtherFields()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Jockey);
        var profile = new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = account.Id, FullName = "Original", Nationality = "VN" };
        db.Accounts.Add(account);
        db.JockeyProfiles.Add(profile);
        await db.SaveChangesAsync();

        JockeyProfileService service = CreateService(db);
        var request = new JockeyProfileUpdateRequest { Weight = 55.5f };

        await service.UpdateJockeyProfileAsync(account.Id, request);

        JockeyProfile updated = await db.JockeyProfiles.AsNoTracking().SingleAsync(p => p.AccountId == account.Id);
        Assert.Equal("Original", updated.FullName);
        Assert.Equal("VN", updated.Nationality);
        Assert.Equal(55.5f, updated.Weight);
    }

    [Fact]
    public async Task UploadImageAsync_ProfileNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyProfileService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.UploadImageAsync(Guid.NewGuid(), Mock.Of<IFormFile>()));
    }

    [Fact]
    public async Task UploadImageAsync_ProfileFound_UpdatesImageUrl()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Jockey);
        var profile = new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = account.Id };
        db.Accounts.Add(account);
        db.JockeyProfiles.Add(profile);
        await db.SaveChangesAsync();

        var mockCloudinary = new Mock<ICloudinaryService>();
        mockCloudinary
            .Setup(c => c.UploadImageAsync(It.IsAny<IFormFile>(), "jockey-profiles"))
            .ReturnsAsync("https://cdn.test/jockey.png");

        JockeyProfileService service = CreateService(db, mockCloudinary);

        string url = await service.UploadImageAsync(account.Id, Mock.Of<IFormFile>());

        Assert.Equal("https://cdn.test/jockey.png", url);
    }

    [Fact]
    public async Task GetJockeyRewardsAsync_ProfileNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyProfileService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetJockeyRewardsAsync(Guid.NewGuid(), 1, 10));
    }

    [Fact]
    public async Task GetAllJockeyProfilesAsync_ExcludesDeletedProfiles()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account1 = NewAccount(AccountRole.Jockey);
        Account account2 = NewAccount(AccountRole.Jockey);
        db.Accounts.AddRange(account1, account2);
        db.JockeyProfiles.Add(new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = account1.Id, FullName = "Active Jockey", IsDeleted = false });
        db.JockeyProfiles.Add(new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = account2.Id, FullName = "Deleted Jockey", IsDeleted = true });
        await db.SaveChangesAsync();

        JockeyProfileService service = CreateService(db);

        List<JockeyProfileResponse> result = await service.GetAllJockeyProfilesAsync();

        Assert.Single(result);
        Assert.Equal("Active Jockey", result[0].FullName);
    }

    [Fact]
    public async Task GetAllJockeyProfilesPagedAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        for (int i = 0; i < 3; i++)
        {
            Account account = NewAccount(AccountRole.Jockey);
            db.Accounts.Add(account);
            db.JockeyProfiles.Add(new JockeyProfile { JockeyProfileId = Guid.NewGuid(), AccountId = account.Id, CreateAt = DateTimeOffset.UtcNow.AddMinutes(i) });
        }
        await db.SaveChangesAsync();

        JockeyProfileService service = CreateService(db);

        PagedResponse<JockeyProfileResponse> result = await service.GetAllJockeyProfilesPagedAsync(page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetJockeyRaceHistoryAsync_ReturnsHistoryWithPositionAndEarnings()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = new() { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "x", Role = AccountRole.HorseOwner, Status = AccountStatus.Active };
        Account jockeyAccount = NewAccount(AccountRole.Jockey);
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished, RaceName = "Grand Prix", StartTime = DateTimeOffset.UtcNow.AddDays(-1) };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockeyAccount.Id, Status = RegistrationStatus.Confirmed };
        var raceResult = new RaceResult { ResultId = Guid.NewGuid(), RegistrationId = registration.RegistrationId, FinishPosition = 1 };
        var prize = new Prize { PrizeId = Guid.NewGuid(), RegistrationId = registration.RegistrationId, PrizeType = PrizeType.Jockey, Amount = 50_000m, DistributedAt = DateTimeOffset.UtcNow };
        db.AddRange(owner, jockeyAccount, racecourse, horse, race, registration, raceResult, prize);
        await db.SaveChangesAsync();

        JockeyProfileService service = CreateService(db);

        PagedResponse<JockeyRaceHistoryItemResponse> result = await service.GetJockeyRaceHistoryAsync(jockeyAccount.Id, page: 1, pageSize: 10);

        Assert.Single(result.Items);
        Assert.Equal(1, result.Items[0].Position);
        Assert.Equal(50_000m, result.Items[0].Earnings);
        Assert.Equal("Grand Prix", result.Items[0].RaceName);
    }
}
