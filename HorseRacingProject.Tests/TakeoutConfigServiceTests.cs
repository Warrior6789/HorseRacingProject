using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingProject.Tests;

public class TakeoutConfigServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static TakeoutConfigService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new TakeoutConfigService(uow);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public async Task CreateAsync_PercentageOutOfRange_ThrowsArgumentException(float percentage)
    {
        using HorseRacingDataContext db = CreateContext();
        TakeoutConfigService service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(new CreateTakeoutConfigRequest { TakeoutPercentage = percentage }));
    }

    [Fact]
    public async Task CreateAsync_Valid_CreatesInactiveConfig()
    {
        using HorseRacingDataContext db = CreateContext();
        TakeoutConfigService service = CreateService(db);

        TakeoutConfigResponse result = await service.CreateAsync(new CreateTakeoutConfigRequest { TakeoutPercentage = 0.2f });

        Assert.Equal(0.2f, result.TakeoutPercentage);
        Assert.Equal(ConfigStatus.Inactive.ToString(), result.Status);
    }

    [Fact]
    public async Task GetActiveConfigAsync_NoActiveConfig_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        TakeoutConfigService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetActiveConfigAsync());
    }

    [Fact]
    public async Task GetActiveConfigAsync_ReturnsActiveConfig()
    {
        using HorseRacingDataContext db = CreateContext();
        db.TakeoutConfigs.Add(new TakeoutConfig { TakeoutConfigId = Guid.NewGuid(), TakeoutPercentage = 0.1f, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow });
        var active = new TakeoutConfig { TakeoutConfigId = Guid.NewGuid(), TakeoutPercentage = 0.25f, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.TakeoutConfigs.Add(active);
        await db.SaveChangesAsync();

        TakeoutConfigService service = CreateService(db);

        TakeoutConfigResponse result = await service.GetActiveConfigAsync();

        Assert.Equal(active.TakeoutConfigId, result.Id);
        Assert.Equal(0.25f, result.TakeoutPercentage);
    }

    [Fact]
    public async Task GetAllPagedAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        for (int i = 0; i < 3; i++)
            db.TakeoutConfigs.Add(new TakeoutConfig { TakeoutConfigId = Guid.NewGuid(), TakeoutPercentage = 0.1f, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i) });
        await db.SaveChangesAsync();

        TakeoutConfigService service = CreateService(db);

        PagedResponse<TakeoutConfigResponse> result = await service.GetAllPagedAsync(page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task SetActiveAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        TakeoutConfigService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SetActiveAsync_OnlyOneConfigActiveAtATime()
    {
        using HorseRacingDataContext db = CreateContext();
        var configA = new TakeoutConfig { TakeoutConfigId = Guid.NewGuid(), TakeoutPercentage = 0.1f, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        var configB = new TakeoutConfig { TakeoutConfigId = Guid.NewGuid(), TakeoutPercentage = 0.2f, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow };
        db.TakeoutConfigs.AddRange(configA, configB);
        await db.SaveChangesAsync();

        TakeoutConfigService service = CreateService(db);

        await service.SetActiveAsync(configB.TakeoutConfigId);

        TakeoutConfig updatedA = await db.TakeoutConfigs.AsNoTracking().SingleAsync(c => c.TakeoutConfigId == configA.TakeoutConfigId);
        TakeoutConfig updatedB = await db.TakeoutConfigs.AsNoTracking().SingleAsync(c => c.TakeoutConfigId == configB.TakeoutConfigId);
        Assert.Equal(ConfigStatus.Inactive, updatedA.Status);
        Assert.Equal(ConfigStatus.Active, updatedB.Status);
    }
}
