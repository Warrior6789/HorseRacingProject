using HorseRacingAPI.Dtos;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Moq;

namespace HorseRacingProject.Tests;

public class InputValidationTests
{
    private static IUnitofWork EmptyUow() => new Mock<IUnitofWork>().Object;
    private static IHubContext<RaceHub> EmptyHub() => new Mock<IHubContext<RaceHub>>().Object;

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(-1_000_000L)]
    public async Task Withdrawal_NonPositiveAmount_ThrowsArgumentException(long amount)
    {
        var service = new WithdrawalService(EmptyUow(), EmptyHub());
        var req = new WithdrawalRequest { Amount = amount };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateWithdrawalAsync(Guid.NewGuid(), req));
    }

    [Fact]
    public async Task Withdrawal_PositiveAmount_DoesNotThrowArgumentException()
    {
        var service = new WithdrawalService(EmptyUow(), EmptyHub());
        var req = new WithdrawalRequest { Amount = 1 };

        var ex = await Record.ExceptionAsync(() =>
            service.CreateWithdrawalAsync(Guid.NewGuid(), req));
        Assert.False(ex is ArgumentException);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Spectator")]
    [InlineData("InvalidRole")]
    [InlineData("")]
    [InlineData("moderator")]
    public async Task RequestRoleUpgrade_InvalidRole_ThrowsArgumentException(string role)
    {
        var service = new AuthService(
            EmptyUow(),
            new Mock<IConfiguration>().Object,
            new Mock<ICloudinaryService>().Object,
            EmptyHub());
        var dto = new RequestRoleUpgradeDto { RequestedRole = role };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RequestRoleUpgradeAsync(Guid.NewGuid(), dto));
    }

    [Theory]
    [InlineData("HorseOwner")]
    [InlineData("Jockey")]
    [InlineData("Referee")]
    [InlineData("horseowner")]
    [InlineData("JOCKEY")]
    public async Task RequestRoleUpgrade_ValidRole_DoesNotThrowArgumentException(string role)
    {
        var service = new AuthService(
            EmptyUow(),
            new Mock<IConfiguration>().Object,
            new Mock<ICloudinaryService>().Object,
            EmptyHub());
        var dto = new RequestRoleUpgradeDto { RequestedRole = role };

        var ex = await Record.ExceptionAsync(() =>
            service.RequestRoleUpgradeAsync(Guid.NewGuid(), dto));
        Assert.False(ex is ArgumentException,
            $"Role '{role}' là hợp lệ, không được throw ArgumentException.");
    }

    [Theory]
    [InlineData("InvalidStatus")]
    [InlineData("Deleted")]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("")]
    [InlineData("unknown")]
    public async Task GetAccountByStatus_InvalidStatus_ThrowsArgumentException(string status)
    {
        var service = new AccountService(EmptyUow(), EmptyHub());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAccountByStatusAsync(status));
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Banned")]
    [InlineData("Suspended")]
    [InlineData("active")]
    [InlineData("BANNED")]
    public async Task GetAccountByStatus_ValidStatus_DoesNotThrowArgumentException(string status)
    {
        var service = new AccountService(EmptyUow(), EmptyHub());

        var ex = await Record.ExceptionAsync(() =>
            service.GetAccountByStatusAsync(status));
        Assert.False(ex is ArgumentException,
            $"Status '{status}' là hợp lệ, không được throw ArgumentException.");
    }
}
