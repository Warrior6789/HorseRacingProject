using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingProject.Tests;

public class PositionPrizeConfigServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static PositionPrizeConfigService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new PositionPrizeConfigService(uow);
    }

    private static CreatePositionPrizeConfigRequest ValidRequest() => new CreatePositionPrizeConfigRequest
    {
        Pos1Ratio = 0.4f,
        Pos2Ratio = 0.25f,
        Pos3Ratio = 0.15f,
        Pos4Ratio = 0.1f,
        Pos5Ratio = 0.05f,
        Pos6Ratio = 0.05f
    };

    [Fact]
    public async Task CreateAsync_SumExceedsOneHundredPercent_ThrowsInvalidOperation()
    {
        using HorseRacingDataContext db = CreateContext();
        PositionPrizeConfigService service = CreateService(db);
        var request = ValidRequest();
        request.Pos6Ratio = 0.5f;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_Valid_CreatesInactiveConfig()
    {
        using HorseRacingDataContext db = CreateContext();
        PositionPrizeConfigService service = CreateService(db);

        PositionPrizeConfigResponse result = await service.CreateAsync(ValidRequest());

        Assert.Equal(0.4f, result.Pos1Ratio);
        Assert.Equal(ConfigStatus.Inactive.ToString(), result.Status);
    }

    [Fact]
    public async Task GetActiveAsync_NoActiveConfig_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        PositionPrizeConfigService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetActiveAsync());
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsActiveConfig()
    {
        using HorseRacingDataContext db = CreateContext();
        var active = new PositionPrizeConfig
        {
            PositionPrizeConfigId = Guid.NewGuid(),
            Pos1Ratio = 0.4f,
            Pos2Ratio = 0.25f,
            Pos3Ratio = 0.15f,
            Pos4Ratio = 0.1f,
            Pos5Ratio = 0.05f,
            Pos6Ratio = 0.05f,
            Status = ConfigStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.PositionPrizeConfigs.Add(active);
        await db.SaveChangesAsync();

        PositionPrizeConfigService service = CreateService(db);

        PositionPrizeConfigResponse result = await service.GetActiveAsync();

        Assert.Equal(active.PositionPrizeConfigId, result.Id);
    }

    [Fact]
    public async Task GetAllPagedAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        for (int i = 0; i < 3; i++)
        {
            db.PositionPrizeConfigs.Add(new PositionPrizeConfig
            {
                PositionPrizeConfigId = Guid.NewGuid(),
                Pos1Ratio = 0.4f,
                Pos2Ratio = 0.25f,
                Pos3Ratio = 0.15f,
                Pos4Ratio = 0.1f,
                Pos5Ratio = 0.05f,
                Pos6Ratio = 0.05f,
                Status = ConfigStatus.Inactive,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        PositionPrizeConfigService service = CreateService(db);

        PagedResponse<PositionPrizeConfigResponse> result = await service.GetAllPagedAsync(page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task SetActiveAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        PositionPrizeConfigService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetActiveAsync(Guid.NewGuid()));
    }

}
