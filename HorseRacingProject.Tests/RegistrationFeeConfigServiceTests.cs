using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingProject.Tests;

public class RegistrationFeeConfigServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static RegistrationFeeConfigService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new RegistrationFeeConfigService(uow);
    }

    [Fact]
    public async Task CreateAsync_CreatesInactiveConfig()
    {
        using HorseRacingDataContext db = CreateContext();
        RegistrationFeeConfigService service = CreateService(db);

        RegistrationFeeConfigResponse result = await service.CreateAsync(new CreateRegistrationFeeConfigRequest { FeeAmount = 50_000 });

        Assert.Equal(50_000, result.FeeAmount);
        Assert.Equal(ConfigStatus.Inactive.ToString(), result.Status);
    }

    [Fact]
    public async Task GetActiveAsync_NoActiveConfig_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RegistrationFeeConfigService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetActiveAsync());
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsActiveConfig()
    {
        using HorseRacingDataContext db = CreateContext();
        db.RegistrationFeeConfigs.Add(new RegistrationFeeConfig { RegistrationFeeConfigId = Guid.NewGuid(), FeeAmount = 10_000, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow });
        var active = new RegistrationFeeConfig { RegistrationFeeConfigId = Guid.NewGuid(), FeeAmount = 20_000, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.RegistrationFeeConfigs.Add(active);
        await db.SaveChangesAsync();

        RegistrationFeeConfigService service = CreateService(db);

        RegistrationFeeConfigResponse result = await service.GetActiveAsync();

        Assert.Equal(active.RegistrationFeeConfigId, result.Id);
        Assert.Equal(20_000, result.FeeAmount);
    }

    [Fact]
    public async Task SetActiveAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RegistrationFeeConfigService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SetActiveAsync_OnlyOneConfigActiveAtATime()
    {
        using HorseRacingDataContext db = CreateContext();
        var configA = new RegistrationFeeConfig { RegistrationFeeConfigId = Guid.NewGuid(), FeeAmount = 10_000, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        var configB = new RegistrationFeeConfig { RegistrationFeeConfigId = Guid.NewGuid(), FeeAmount = 20_000, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow };
        db.RegistrationFeeConfigs.AddRange(configA, configB);
        await db.SaveChangesAsync();

        RegistrationFeeConfigService service = CreateService(db);

        await service.SetActiveAsync(configB.RegistrationFeeConfigId);

        RegistrationFeeConfig updatedA = await db.RegistrationFeeConfigs.AsNoTracking().SingleAsync(c => c.RegistrationFeeConfigId == configA.RegistrationFeeConfigId);
        RegistrationFeeConfig updatedB = await db.RegistrationFeeConfigs.AsNoTracking().SingleAsync(c => c.RegistrationFeeConfigId == configB.RegistrationFeeConfigId);
        Assert.Equal(ConfigStatus.Inactive, updatedA.Status);
        Assert.Equal(ConfigStatus.Active, updatedB.Status);
    }

    [Fact]
    public async Task GetAllPagedAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        for (int i = 0; i < 3; i++)
            db.RegistrationFeeConfigs.Add(new RegistrationFeeConfig { RegistrationFeeConfigId = Guid.NewGuid(), FeeAmount = i * 1000, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i) });
        await db.SaveChangesAsync();

        RegistrationFeeConfigService service = CreateService(db);

        PagedResponse<RegistrationFeeConfigResponse> result = await service.GetAllPagedAsync(page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }
}
