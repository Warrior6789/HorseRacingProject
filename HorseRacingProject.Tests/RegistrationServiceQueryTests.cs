using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HorseRacingProject.Tests;

public class RegistrationServiceQueryTests
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

    private static RegistrationService CreateService(HorseRacingDataContext db)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new RegistrationService(uow, CreateHubContext());
    }

    private static Racecourse NewRacecourse() => new Racecourse { Id = Guid.NewGuid(), RacecourseName = $"Track-{Guid.NewGuid():N}" };

    private static Account NewAccount(AccountRole role) => new Account
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@test.com",
        PasswordHash = "x",
        Role = role,
        Status = AccountStatus.Active
    };

    private static Horse NewHorse(Guid ownerId) => new Horse { Id = Guid.NewGuid(), OwnerId = ownerId, HorseName = "Thunder", Status = HorseStatus.Healthy };

    [Fact]
    public async Task GetMyRequestAsync_ReturnsOnlyPendingAwaitingJockeyConfirmation()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Horse horse1 = NewHorse(owner.Id);
        Horse horse2 = NewHorse(owner.Id);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        var awaitingReg = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse1.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Pending, OwnerConfirmation = true, JockeyConfirmation = null };
        var confirmedReg = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse2.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed, OwnerConfirmation = true, JockeyConfirmation = true };
        db.AddRange(racecourse, owner, jockey, horse1, horse2, race, awaitingReg, confirmedReg);
        await db.SaveChangesAsync();
        RegistrationService service = CreateService(db);

        List<RegistrationResponse> result = await service.GetMyRequestAsync(jockey.Id);

        Assert.Single(result);
        Assert.Equal(awaitingReg.RegistrationId, result[0].RegistrationId);
    }

    [Fact]
    public async Task GetMyRequestPagedAsync_IncludesPendingAndConfirmedForJockey()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Horse horse1 = NewHorse(owner.Id);
        Horse horse2 = NewHorse(owner.Id);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        var pendingReg = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse1.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Pending, OwnerConfirmation = true, JockeyConfirmation = null };
        var confirmedReg = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse2.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed, OwnerConfirmation = true, JockeyConfirmation = true };
        var rejectedReg = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse2.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Rejected, OwnerConfirmation = true, JockeyConfirmation = false };
        db.AddRange(racecourse, owner, jockey, horse1, horse2, race, pendingReg, confirmedReg, rejectedReg);
        await db.SaveChangesAsync();
        RegistrationService service = CreateService(db);

        PagedResponse<RegistrationResponse> result = await service.GetMyRequestPagedAsync(jockey.Id, page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetAllOwnerRegistrationsAsync_ReturnsAllStatusesOrderedByCreateAtDescending()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Horse horse1 = NewHorse(owner.Id);
        Horse horse2 = NewHorse(owner.Id);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        var older = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse1.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Rejected, CreateAt = DateTimeOffset.UtcNow.AddHours(-2) };
        var newer = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse2.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed, CreateAt = DateTimeOffset.UtcNow.AddHours(-1) };
        db.AddRange(racecourse, owner, jockey, horse1, horse2, race, older, newer);
        await db.SaveChangesAsync();
        RegistrationService service = CreateService(db);

        List<RegistrationResponse> result = await service.GetAllOwnerRegistrationsAsync(owner.Id);

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.RegistrationId, result[0].RegistrationId);
        Assert.Equal(older.RegistrationId, result[1].RegistrationId);
    }

    [Fact]
    public async Task GetAllOwnerRegistrationsPagedAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        db.AddRange(racecourse, owner, jockey, race);
        for (int i = 0; i < 3; i++)
        {
            Horse horse = NewHorse(owner.Id);
            db.Horses.Add(horse);
            db.Registrations.Add(new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Confirmed, CreateAt = DateTimeOffset.UtcNow.AddMinutes(i) });
        }
        await db.SaveChangesAsync();
        RegistrationService service = CreateService(db);

        PagedResponse<RegistrationResponse> result = await service.GetAllOwnerRegistrationsPagedAsync(owner.Id, page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetAllRegistrationsPagedAsync_FiltersByRaceId()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        Horse horse1 = NewHorse(owner.Id);
        Horse horse2 = NewHorse(owner.Id);
        var race1 = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        var race2 = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(3) };
        var reg1 = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race1.RaceId, HorseId = horse1.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Pending };
        var reg2 = new Registration { RegistrationId = Guid.NewGuid(), RaceId = race2.RaceId, HorseId = horse2.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Pending };
        db.AddRange(racecourse, owner, jockey, horse1, horse2, race1, race2, reg1, reg2);
        await db.SaveChangesAsync();
        RegistrationService service = CreateService(db);

        PagedResponse<RegistrationResponse> result = await service.GetAllRegistrationsPagedAsync(page: 1, pageSize: 10, raceId: race1.RaceId);

        Assert.Single(result.Items);
        Assert.Equal(reg1.RegistrationId, result.Items[0].RegistrationId);
    }

    [Fact]
    public async Task GetAllRegistrationsPagedAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        Racecourse racecourse = NewRacecourse();
        Account owner = NewAccount(AccountRole.HorseOwner);
        Account jockey = NewAccount(AccountRole.Jockey);
        var race = new Race { RaceId = Guid.NewGuid(), RacecourseId = racecourse.Id, Status = RaceStatus.Scheduled, StartTime = DateTimeOffset.UtcNow.AddHours(2) };
        db.AddRange(racecourse, owner, jockey, race);
        for (int i = 0; i < 3; i++)
        {
            Horse horse = NewHorse(owner.Id);
            db.Horses.Add(horse);
            db.Registrations.Add(new Registration { RegistrationId = Guid.NewGuid(), RaceId = race.RaceId, HorseId = horse.Id, JockeyId = jockey.Id, Status = RegistrationStatus.Pending });
        }
        await db.SaveChangesAsync();
        RegistrationService service = CreateService(db);

        PagedResponse<RegistrationResponse> result = await service.GetAllRegistrationsPagedAsync(page: 0, pageSize: -5);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }
}
