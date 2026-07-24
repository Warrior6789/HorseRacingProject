using HorseRacingAPI.Dtos;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Moq;

namespace HorseRacingProject.Tests;

public class ConfigValidationTests
{
    private static IUnitofWork EmptyUow() => new Mock<IUnitofWork>().Object;

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(-1.0f)]
    [InlineData(1.0f)]
    [InlineData(1.5f)]
    public async Task TakeoutConfig_InvalidPercentage_ThrowsArgumentException(float percentage)
    {
        var service = new TakeoutConfigService(EmptyUow());
        var req = new CreateTakeoutConfigRequest { TakeoutPercentage = percentage };

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(req));
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.20f)]
    [InlineData(0.9999f)]
    public async Task TakeoutConfig_ValidPercentage_DoesNotThrowValidationError(float percentage)
    {
        var uow = new Mock<IUnitofWork>();
        var mockRepo = new Mock<HorseRacingAPI.Repositories.IGenericRepository<HorseRacingAPI.Models.TakeoutConfig>>();
        mockRepo.Setup(r => r.AddAsync(It.IsAny<HorseRacingAPI.Models.TakeoutConfig>())).Returns(Task.CompletedTask);
        uow.Setup(u => u.GetRepository<HorseRacingAPI.Models.TakeoutConfig>()).Returns(mockRepo.Object);
        uow.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        var service = new TakeoutConfigService(uow.Object);
        var req = new CreateTakeoutConfigRequest { TakeoutPercentage = percentage };

        var ex = await Record.ExceptionAsync(() => service.CreateAsync(req));
        Assert.False(ex is ArgumentException, $"Không được throw ArgumentException với percentage={percentage}");
    }

    [Theory]
    [InlineData(0.05f, 0.10f)]
    [InlineData(0.0f, 0.01f)]
    [InlineData(0.09f, 0.10f)]
    public async Task JockeyRewardConfig_WinCutLessThanPlaceCut_ThrowsInvalidOperationException(float winCut, float placeCut)
    {
        var service = new JockeyRewardConfigService(EmptyUow());
        var req = new CreateJockeyRewardConfigRequest { WinCut = winCut, PlaceCut = placeCut };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req));
    }

    [Theory]
    [InlineData(0.10f, 0.10f)]
    [InlineData(0.15f, 0.10f)]
    [InlineData(0.20f, 0.05f)]
    public async Task JockeyRewardConfig_ValidCuts_DoesNotThrowValidationError(float winCut, float placeCut)
    {
        var uow = new Mock<IUnitofWork>();
        var mockRepo = new Mock<HorseRacingAPI.Repositories.IGenericRepository<HorseRacingAPI.Models.JockeyRewardConfig>>();
        mockRepo.Setup(r => r.AddAsync(It.IsAny<HorseRacingAPI.Models.JockeyRewardConfig>())).Returns(Task.CompletedTask);
        uow.Setup(u => u.GetRepository<HorseRacingAPI.Models.JockeyRewardConfig>()).Returns(mockRepo.Object);
        uow.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        var service = new JockeyRewardConfigService(uow.Object);
        var req = new CreateJockeyRewardConfigRequest { WinCut = winCut, PlaceCut = placeCut };

        var ex = await Record.ExceptionAsync(() => service.CreateAsync(req));
        Assert.False(ex is InvalidOperationException, $"Không được throw InvalidOperationException với WinCut={winCut}, PlaceCut={placeCut}");
    }

    [Theory]
    [InlineData(0.50f, 0.30f, 0.10f, 0.05f, 0.04f, 0.03f)]
    [InlineData(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0002f)]
    [InlineData(0.4f, 0.3f, 0.2f, 0.1f, 0.05f, 0.05f)]
    public async Task PositionPrizeConfig_SumExceeds100_ThrowsInvalidOperationException(
        float p1, float p2, float p3, float p4, float p5, float p6)
    {
        var service = new PositionPrizeConfigService(EmptyUow());
        var req = new CreatePositionPrizeConfigRequest
        {
            Pos1Ratio = p1, Pos2Ratio = p2, Pos3Ratio = p3,
            Pos4Ratio = p4, Pos5Ratio = p5, Pos6Ratio = p6
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req));
    }

    [Theory]
    [InlineData(0.50f, 0.30f, 0.10f, 0.05f, 0.03f, 0.02f)]
    [InlineData(0.30f, 0.20f, 0.10f, 0.0f, 0.0f, 0.0f)]
    [InlineData(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f)]
    public async Task PositionPrizeConfig_ValidSum_DoesNotThrowValidationError(
        float p1, float p2, float p3, float p4, float p5, float p6)
    {
        var uow = new Mock<IUnitofWork>();
        var mockRepo = new Mock<HorseRacingAPI.Repositories.IGenericRepository<HorseRacingAPI.Models.PositionPrizeConfig>>();
        mockRepo.Setup(r => r.AddAsync(It.IsAny<HorseRacingAPI.Models.PositionPrizeConfig>())).Returns(Task.CompletedTask);
        uow.Setup(u => u.GetRepository<HorseRacingAPI.Models.PositionPrizeConfig>()).Returns(mockRepo.Object);
        uow.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        var service = new PositionPrizeConfigService(uow.Object);
        var req = new CreatePositionPrizeConfigRequest
        {
            Pos1Ratio = p1, Pos2Ratio = p2, Pos3Ratio = p3,
            Pos4Ratio = p4, Pos5Ratio = p5, Pos6Ratio = p6
        };

        var ex = await Record.ExceptionAsync(() => service.CreateAsync(req));
        Assert.False(ex is InvalidOperationException ie && ie.Message.Contains("must not exceed"),
            $"Không được throw validation error với sum hợp lệ");
    }
}
