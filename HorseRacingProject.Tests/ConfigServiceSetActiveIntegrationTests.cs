using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingProject.Tests;

[Collection("Postgres")]
public class ConfigServiceSetActiveIntegrationTests
{
    private readonly PostgresContainerFixture _fixture;

    public ConfigServiceSetActiveIntegrationTests(PostgresContainerFixture fixture)
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

    [Fact]
    public async Task PositionPrizeConfigService_SetActiveAsync_DeactivatesPreviousAndActivatesTarget()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();

        var configA = new PositionPrizeConfig { PositionPrizeConfigId = Guid.NewGuid(), Pos1Ratio = 0.5f, Pos2Ratio = 0.3f, Pos3Ratio = 0.2f, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        var configB = new PositionPrizeConfig { PositionPrizeConfigId = Guid.NewGuid(), Pos1Ratio = 0.6f, Pos2Ratio = 0.3f, Pos3Ratio = 0.1f, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow };
        db.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var service = new PositionPrizeConfigService(new UnitofWork(db));
        await service.SetActiveAsync(configB.PositionPrizeConfigId);

        PositionPrizeConfig reloadedA = await db.PositionPrizeConfigs.AsNoTracking().SingleAsync(c => c.PositionPrizeConfigId == configA.PositionPrizeConfigId);
        PositionPrizeConfig reloadedB = await db.PositionPrizeConfigs.AsNoTracking().SingleAsync(c => c.PositionPrizeConfigId == configB.PositionPrizeConfigId);
        Assert.Equal(ConfigStatus.Inactive, reloadedA.Status);
        Assert.Equal(ConfigStatus.Active, reloadedB.Status);
    }

    [Fact]
    public async Task PositionPrizeConfigService_SetActiveAsync_NotFound_Throws()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        var service = new PositionPrizeConfigService(new UnitofWork(db));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task JockeyRewardConfigService_SetActiveAsync_DeactivatesPreviousAndActivatesTarget()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();

        var configA = new JockeyRewardConfig { JockeyRewardConfigId = Guid.NewGuid(), WinCut = 0.10f, PlaceCut = 0.05f, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        var configB = new JockeyRewardConfig { JockeyRewardConfigId = Guid.NewGuid(), WinCut = 0.15f, PlaceCut = 0.08f, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow };
        db.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var service = new JockeyRewardConfigService(new UnitofWork(db));
        await service.SetActiveAsync(configB.JockeyRewardConfigId);

        JockeyRewardConfig reloadedA = await db.JockeyRewardConfigs.AsNoTracking().SingleAsync(c => c.JockeyRewardConfigId == configA.JockeyRewardConfigId);
        JockeyRewardConfig reloadedB = await db.JockeyRewardConfigs.AsNoTracking().SingleAsync(c => c.JockeyRewardConfigId == configB.JockeyRewardConfigId);
        Assert.Equal(ConfigStatus.Inactive, reloadedA.Status);
        Assert.Equal(ConfigStatus.Active, reloadedB.Status);
    }

    [Fact]
    public async Task JockeyRewardConfigService_SetActiveAsync_NotFound_Throws()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        var service = new JockeyRewardConfigService(new UnitofWork(db));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task TakeoutConfigService_SetActiveAsync_DeactivatesPreviousAndActivatesTarget()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();

        var configA = new TakeoutConfig { TakeoutConfigId = Guid.NewGuid(), TakeoutPercentage = 0.10f, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        var configB = new TakeoutConfig { TakeoutConfigId = Guid.NewGuid(), TakeoutPercentage = 0.20f, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow };
        db.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var service = new TakeoutConfigService(new UnitofWork(db));
        await service.SetActiveAsync(configB.TakeoutConfigId);

        TakeoutConfig reloadedA = await db.TakeoutConfigs.AsNoTracking().SingleAsync(c => c.TakeoutConfigId == configA.TakeoutConfigId);
        TakeoutConfig reloadedB = await db.TakeoutConfigs.AsNoTracking().SingleAsync(c => c.TakeoutConfigId == configB.TakeoutConfigId);
        Assert.Equal(ConfigStatus.Inactive, reloadedA.Status);
        Assert.Equal(ConfigStatus.Active, reloadedB.Status);
    }

    [Fact]
    public async Task TakeoutConfigService_SetActiveAsync_NotFound_Throws()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        var service = new TakeoutConfigService(new UnitofWork(db));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RegistrationFeeConfigService_SetActiveAsync_DeactivatesPreviousAndActivatesTarget()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();

        var configA = new RegistrationFeeConfig { RegistrationFeeConfigId = Guid.NewGuid(), FeeAmount = 1_000m, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        var configB = new RegistrationFeeConfig { RegistrationFeeConfigId = Guid.NewGuid(), FeeAmount = 2_000m, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow };
        db.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var service = new RegistrationFeeConfigService(new UnitofWork(db));
        await service.SetActiveAsync(configB.RegistrationFeeConfigId);

        RegistrationFeeConfig reloadedA = await db.RegistrationFeeConfigs.AsNoTracking().SingleAsync(c => c.RegistrationFeeConfigId == configA.RegistrationFeeConfigId);
        RegistrationFeeConfig reloadedB = await db.RegistrationFeeConfigs.AsNoTracking().SingleAsync(c => c.RegistrationFeeConfigId == configB.RegistrationFeeConfigId);
        Assert.Equal(ConfigStatus.Inactive, reloadedA.Status);
        Assert.Equal(ConfigStatus.Active, reloadedB.Status);
    }

    [Fact]
    public async Task RegistrationFeeConfigService_SetActiveAsync_NotFound_Throws()
    {
        await using HorseRacingDataContext db = await CreateContextAsync();
        var service = new RegistrationFeeConfigService(new UnitofWork(db));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid()));
    }
}
