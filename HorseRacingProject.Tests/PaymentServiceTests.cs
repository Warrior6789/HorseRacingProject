using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace HorseRacingProject.Tests;

public class PaymentServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static IHubContext<RaceHub> CreateHubContext()
    {
        var clientProxy = new Mock<IClientProxy>();
        clientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<RaceHub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        return hubContext.Object;
    }

    private static IConfiguration CreateConfig()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["PayOS:ClientId"] = "test-client-id",
            ["PayOS:ApiKey"] = "test-api-key",
            ["PayOS:ChecksumKey"] = "test-checksum-key",
            ["PayOS:ReturnBaseUrl"] = "https://test.local"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
    }

    private static PaymentService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new PaymentService(uow, CreateConfig(), CreateHubContext());
    }

    [Fact]
    public async Task CreateDepositUrlAsync_AccountNotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        PaymentService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateDepositUrlAsync(Guid.NewGuid(), new DepositRequest { Amount = 50_000 }, "127.0.0.1"));
    }

    [Fact]
    public async Task GetHistoryPagingAsync_FiltersByAccountId()
    {
        using HorseRacingDataContext db = CreateContext();
        Guid accountId = Guid.NewGuid();
        Guid otherAccountId = Guid.NewGuid();
        db.Accounts.Add(new Account { Id = accountId, Email = "owner@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active });
        db.Accounts.Add(new Account { Id = otherAccountId, Email = "other@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active });
        db.Payments.Add(new Payment { PaymentId = Guid.NewGuid(), AccountId = accountId, Amount = 10_000, OrderCode = 1, Status = PaymentStatus.Completed, CreateAt = DateTimeOffset.UtcNow, BalanceChanged = 10_000, CurrentBalance = 10_000 });
        db.Payments.Add(new Payment { PaymentId = Guid.NewGuid(), AccountId = otherAccountId, Amount = 20_000, OrderCode = 2, Status = PaymentStatus.Completed, CreateAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        PaymentService service = CreateService(db);

        PagedResponse<PaymentResponse> result = await service.GetHistoryPagingAsync(accountId, page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Single(result.Items);
        Assert.Equal(10_000, result.Items[0].Amount);
    }

    [Fact]
    public async Task GetAllPaymentsPagingAsync_ReturnsAllPaymentsClamped()
    {
        using HorseRacingDataContext db = CreateContext();
        for (int i = 0; i < 3; i++)
        {
            Guid accountId = Guid.NewGuid();
            db.Accounts.Add(new Account { Id = accountId, Email = $"account{i}@test.com", PasswordHash = "x", Role = AccountRole.Spectator, Status = AccountStatus.Active });
            db.Payments.Add(new Payment { PaymentId = Guid.NewGuid(), AccountId = accountId, Amount = 10_000, OrderCode = i, Status = PaymentStatus.Completed, CreateAt = DateTimeOffset.UtcNow.AddMinutes(i) });
        }
        await db.SaveChangesAsync();
        PaymentService service = CreateService(db);

        PagedResponse<PaymentResponse> result = await service.GetAllPaymentsPagingAsync(page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }
}
