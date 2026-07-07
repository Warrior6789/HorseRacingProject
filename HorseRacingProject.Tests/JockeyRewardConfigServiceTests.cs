using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingProject.Tests;

public class JockeyRewardConfigServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static JockeyRewardConfigService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new JockeyRewardConfigService(uow);
    }

    [Fact]
    public async Task CreateAsync_WinCutLessThanPlaceCut_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyRewardConfigService service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(new CreateJockeyRewardConfigRequest { WinCut = 0.1f, PlaceCut = 0.2f }));
    }

    [Fact]
    public async Task CreateAsync_Valid_CreatesInactiveConfig()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyRewardConfigService service = CreateService(db);

        JockeyRewardConfigResponse result = await service.CreateAsync(new CreateJockeyRewardConfigRequest { WinCut = 0.2f, PlaceCut = 0.1f });

        Assert.Equal(0.2f, result.WinCut);
        Assert.Equal(0.1f, result.PlaceCut);
        Assert.Equal(ConfigStatus.Inactive.ToString(), result.Status);
    }

    [Fact]
    public async Task GetActiveAsync_NoActiveConfig_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyRewardConfigService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetActiveAsync());
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsActiveConfig()
    {
        using HorseRacingDataContext db = CreateContext();
        db.JockeyRewardConfigs.Add(new JockeyRewardConfig { JockeyRewardConfigId = Guid.NewGuid(), WinCut = 0.1f, PlaceCut = 0.05f, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow });
        var active = new JockeyRewardConfig { JockeyRewardConfigId = Guid.NewGuid(), WinCut = 0.2f, PlaceCut = 0.1f, Status = ConfigStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.JockeyRewardConfigs.Add(active);
        await db.SaveChangesAsync();

        JockeyRewardConfigService service = CreateService(db);

        JockeyRewardConfigResponse result = await service.GetActiveAsync();

        Assert.Equal(active.JockeyRewardConfigId, result.Id);
        Assert.Equal(0.2f, result.WinCut);
    }

    [Fact]
    public async Task GetAllPagedAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        for (int i = 0; i < 3; i++)
            db.JockeyRewardConfigs.Add(new JockeyRewardConfig { JockeyRewardConfigId = Guid.NewGuid(), WinCut = 0.1f, PlaceCut = 0.05f, Status = ConfigStatus.Inactive, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i) });
        await db.SaveChangesAsync();

        JockeyRewardConfigService service = CreateService(db);

        PagedResponse<JockeyRewardConfigResponse> result = await service.GetAllPagedAsync(page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task SetActiveAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        JockeyRewardConfigService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid()));
    }

}
