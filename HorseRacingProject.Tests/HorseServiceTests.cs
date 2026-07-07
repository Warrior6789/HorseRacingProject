using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Middlewares;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HorseRacingProject.Tests;

public class HorseServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static HorseService CreateService(HorseRacingDataContext db, Mock<ICloudinaryService>? cloudinary = null)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new HorseService(uow, (cloudinary ?? new Mock<ICloudinaryService>()).Object);
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
    public async Task CreateHorseAsync_AdminWithoutOwnerId_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        HorseService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateHorseAsync(Guid.NewGuid(), isAdmin: true, new HorseCreateRequest { HorseName = "Thunder" }));
    }

    [Fact]
    public async Task CreateHorseAsync_OwnerAccountNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        HorseService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateHorseAsync(Guid.NewGuid(), isAdmin: false, new HorseCreateRequest { HorseName = "Thunder" }));
    }

    [Fact]
    public async Task CreateHorseAsync_AccountNotHorseOwnerRole_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        Account account = NewAccount(AccountRole.Spectator);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateHorseAsync(account.Id, isAdmin: false, new HorseCreateRequest { HorseName = "Thunder" }));
    }

    [Fact]
    public async Task CreateHorseAsync_Valid_CreatesHealthyHorseWithZeroWins()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        db.Accounts.Add(owner);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);
        var request = new HorseCreateRequest { HorseName = "Thunder", Breed = "Arabian" };

        HorseDetailResponse result = await service.CreateHorseAsync(owner.Id, isAdmin: false, request);

        Assert.Equal("Thunder", result.HorseName);
        Assert.Equal(HorseStatus.Healthy.ToString(), result.Status);
        Assert.Equal(0, result.RecordWins);
    }

    [Fact]
    public async Task GetHorseByIdAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        HorseService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetHorseByIdAsync(Guid.NewGuid(), Guid.NewGuid(), isAdmin: false));
    }

    [Fact]
    public async Task GetHorseByIdAsync_NonAdminNotOwner_ThrowsForbidden()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        db.Accounts.Add(owner);
        db.Horses.Add(horse);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.GetHorseByIdAsync(horse.Id, Guid.NewGuid(), isAdmin: false));
    }

    [Fact]
    public async Task GetHorseByIdAsync_Admin_CanAccessAnyHorse()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        db.Accounts.Add(owner);
        db.Horses.Add(horse);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        HorseDetailResponse result = await service.GetHorseByIdAsync(horse.Id, Guid.NewGuid(), isAdmin: true);

        Assert.Equal("Thunder", result.HorseName);
    }

    [Fact]
    public async Task GetHorseByIdAsync_ConfirmedInLiveRace_DerivedStatusIsRacing()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Live, StartTime = DateTimeOffset.UtcNow.AddMinutes(-5) };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };

        db.AddRange(owner, jockey, racecourse, horse, race, registration);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        HorseDetailResponse result = await service.GetHorseByIdAsync(horse.Id, owner.Id, isAdmin: false);

        Assert.Equal("Racing", result.DerivedStatus);
    }

    [Fact]
    public async Task UpdateHorseAsync_PartialUpdate_KeepsOtherFields()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Breed = "Arabian", Status = HorseStatus.Healthy };
        db.Accounts.Add(owner);
        db.Horses.Add(horse);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);
        var request = new HorseUpdateRequest { Status = "Injury" };

        HorseDetailResponse result = await service.UpdateHorseAsync(horse.Id, owner.Id, isAdmin: false, request);

        Assert.Equal("Thunder", result.HorseName);
        Assert.Equal("Arabian", result.Breed);
        Assert.Equal(HorseStatus.Injury.ToString(), result.Status);
    }

    [Fact]
    public async Task DeleteHorseAsync_SoftDeletes()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        db.Accounts.Add(owner);
        db.Horses.Add(horse);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        await service.DeleteHorseAsync(horse.Id, owner.Id, isAdmin: false);

        Horse updated = await db.Horses.AsNoTracking().SingleAsync(h => h.Id == horse.Id);
        Assert.True(updated.IsDeleted);
    }

    [Fact]
    public async Task GetActiveHorsesAsync_ExcludesInjuryAndRetired()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        db.Accounts.Add(owner);
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Healthy1", Status = HorseStatus.Healthy });
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Resting1", Status = HorseStatus.Resting });
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Injured1", Status = HorseStatus.Injury });
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Retired1", Status = HorseStatus.Retired });
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        List<HorseResponse> result = await service.GetActiveHorsesAsync(owner.Id, isAdmin: false);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, h => h.HorseName == "Healthy1");
        Assert.Contains(result, h => h.HorseName == "Resting1");
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_AggregatesRacesWinsAndEarnings()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };

        var race1 = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished };
        var reg1 = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race1.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        var result1 = new RaceResult { ResultId = Guid.NewGuid(), RegistrationId = reg1.RegistrationId, FinishPosition = 1, IsDisqualified = false };
        var prize1 = new Prize { PrizeId = Guid.NewGuid(), RegistrationId = reg1.RegistrationId, PrizeType = PrizeType.Owner, Amount = 500_000m };

        var race2 = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished };
        var reg2 = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race2.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        var result2 = new RaceResult { ResultId = Guid.NewGuid(), RegistrationId = reg2.RegistrationId, FinishPosition = 3, IsDisqualified = false };

        db.AddRange(owner, jockey, racecourse, horse, race1, reg1, result1, prize1, race2, reg2, result2);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        HorsePerformanceSummaryResponse summary = await service.GetPerformanceSummaryAsync(horse.Id, owner.Id, isAdmin: false);

        Assert.Equal(2, summary.TotalRaces);
        Assert.Equal(1, summary.TotalWins);
        Assert.Equal(500_000m, summary.TotalEarned);
    }

    [Fact]
    public async Task UploadImageAsync_UpdatesImageUrlAndDeletesOldImage()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy, ImageUrl = "https://cdn.test/old/abc.jpg" };
        db.Accounts.Add(owner);
        db.Horses.Add(horse);
        await db.SaveChangesAsync();

        var mockCloudinary = new Mock<ICloudinaryService>();
        mockCloudinary
            .Setup(c => c.UploadImageAsync(It.IsAny<IFormFile>(), "horses"))
            .ReturnsAsync("https://cdn.test/new/def.jpg");

        HorseService service = CreateService(db, mockCloudinary);

        string url = await service.UploadImageAsync(horse.Id, owner.Id, isAdmin: false, Mock.Of<IFormFile>());

        Assert.Equal("https://cdn.test/new/def.jpg", url);
        mockCloudinary.Verify(c => c.DeleteImageAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GetHorsesAsync_NonAdmin_OnlySeesOwnHorses()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account otherOwner = NewAccount(AccountRole.HorseOwner);
        db.Accounts.AddRange(owner, otherOwner);
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy });
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = otherOwner.Id, HorseName = "Bolt", Status = HorseStatus.Healthy });
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);
        var query = new HorseQueryRequest { Page = 0, PageSize = -5 };

        PagedResponse<HorseDetailResponse> result = await service.GetHorsesAsync(owner.Id, isAdmin: false, query);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Single(result.Items);
        Assert.Equal("Thunder", result.Items[0].HorseName);
    }

    [Fact]
    public async Task GetHorsesAsync_FiltersBySearchTerm()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        db.Accounts.Add(owner);
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Breed = "Arabian", Status = HorseStatus.Healthy });
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Bolt", Breed = "Mustang", Status = HorseStatus.Healthy });
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);
        var query = new HorseQueryRequest { Search = "arabian" };

        PagedResponse<HorseDetailResponse> result = await service.GetHorsesAsync(owner.Id, isAdmin: true, query);

        Assert.Single(result.Items);
        Assert.Equal("Thunder", result.Items[0].HorseName);
    }

    [Fact]
    public async Task GetActiveHorsesPagedAsync_ExcludesInjuryAndRetired_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        db.Accounts.Add(owner);
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Healthy1", Status = HorseStatus.Healthy });
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Injured1", Status = HorseStatus.Injury });
        db.Horses.Add(new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Retired1", Status = HorseStatus.Retired });
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        PagedResponse<HorseResponse> result = await service.GetActiveHorsesPagedAsync(owner.Id, isAdmin: false, page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Single(result.Items);
        Assert.Equal("Healthy1", result.Items[0].HorseName);
    }

    [Fact]
    public async Task GetMyScheduleAsync_ReturnsRegistrationsForOwnerHorses()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        db.AddRange(owner, jockey, racecourse, horse, race, registration);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        List<HorseScheduleResponse> result = await service.GetMyScheduleAsync(owner.Id);

        Assert.Single(result);
        Assert.Equal(horse.Id, result[0].HorseId);
    }

    [Fact]
    public async Task GetHorseScheduleAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        HorseService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetHorseScheduleAsync(Guid.NewGuid(), Guid.NewGuid(), isAdmin: false));
    }

    [Fact]
    public async Task GetHorseScheduleAsync_ReturnsScheduleForHorse()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        db.AddRange(owner, jockey, racecourse, horse, race, registration);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        List<HorseScheduleResponse> result = await service.GetHorseScheduleAsync(horse.Id, owner.Id, isAdmin: false);

        Assert.Single(result);
        Assert.Equal(race.RaceId, result[0].RaceId);
    }

    [Fact]
    public async Task GetHorseRewardsAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        HorseService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetHorseRewardsAsync(Guid.NewGuid(), Guid.NewGuid(), isAdmin: false, new HorseRewardsQueryRequest()));
    }

    [Fact]
    public async Task GetHorseRewardsAsync_AggregatesRewardAmounts()
    {
        using HorseRacingDataContext db = CreateContext();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Track" };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, HorseName = "Thunder", Status = HorseStatus.Healthy };
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Finished, StartTime = DateTimeOffset.UtcNow.AddHours(-2) };
        var registration = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed };
        var prize = new Prize { PrizeId = Guid.NewGuid(), RegistrationId = registration.RegistrationId, PrizeType = PrizeType.Owner, Amount = 200_000m, DistributedAt = DateTimeOffset.UtcNow };
        db.AddRange(owner, jockey, racecourse, horse, race, registration, prize);
        await db.SaveChangesAsync();

        HorseService service = CreateService(db);

        HorseRewardsResponse result = await service.GetHorseRewardsAsync(horse.Id, owner.Id, isAdmin: false, new HorseRewardsQueryRequest());

        Assert.Equal(200_000m, result.TotalRewardAmount);
        Assert.Equal(1, result.RewardCount);
        Assert.Single(result.Rewards.Items);
    }
}
