using HorseRacingAPI.Dtos;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HorseRacingProject.Tests;

public class RacecourseServiceTests
{
    private static HorseRacingDataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorseRacingDataContext(options);
    }

    private static RacecourseService CreateService(HorseRacingDataContext db, Mock<ICloudinaryService>? cloudinary = null)
    {
        IUnitofWork uow = new UnitofWork(db);
        return new RacecourseService(uow, (cloudinary ?? new Mock<ICloudinaryService>()).Object);
    }

    [Fact]
    public async Task GetAllRacecoursesAsync_ExcludesDeleted()
    {
        using HorseRacingDataContext db = CreateContext();
        db.Racecourses.Add(new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Active Track" });
        db.Racecourses.Add(new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Deleted Track", IsDeleted = true });
        await db.SaveChangesAsync();

        RacecourseService service = CreateService(db);

        List<RacecourseResponse> result = await service.GetAllRacecoursesAsync();

        Assert.Single(result);
        Assert.Equal("Active Track", result[0].RacecourseName);
    }

    [Fact]
    public async Task CreateRacecourseAsync_NoImage_CreatesWithNullImageUrl()
    {
        using HorseRacingDataContext db = CreateContext();
        RacecourseService service = CreateService(db);
        var request = new CreateRacecourseRequest { RacecourseName = "New Track", Location = "HN", TrackType = "Dirt" };

        RacecourseResponse result = await service.CreateRacecourseAsync(request);

        Assert.Equal("New Track", result.RacecourseName);
        Assert.Null(result.ImageUrl);
        Assert.Equal(1, await db.Racecourses.CountAsync());
    }

    [Fact]
    public async Task CreateRacecourseAsync_WithImage_UploadsToCloudinary()
    {
        using HorseRacingDataContext db = CreateContext();
        var mockCloudinary = new Mock<ICloudinaryService>();
        mockCloudinary
            .Setup(c => c.UploadImageAsync(It.IsAny<IFormFile>(), "racecourses"))
            .ReturnsAsync("https://cdn.test/course.png");

        RacecourseService service = CreateService(db, mockCloudinary);
        var request = new CreateRacecourseRequest { RacecourseName = "New Track", Image = Mock.Of<IFormFile>() };

        RacecourseResponse result = await service.CreateRacecourseAsync(request);

        Assert.Equal("https://cdn.test/course.png", result.ImageUrl);
    }

    [Fact]
    public async Task UpdateRacecourseAsync_NotFound_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        RacecourseService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.UpdateRacecourseAsync(Guid.NewGuid(), new UpdateRacecourseRequest()));
    }

    [Fact]
    public async Task UpdateRacecourseAsync_PartialUpdate_OnlyChangesProvidedFields()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Old Name", Location = "Old Location", TrackType = "Dirt" };
        db.Racecourses.Add(racecourse);
        await db.SaveChangesAsync();

        RacecourseService service = CreateService(db);
        var request = new UpdateRacecourseRequest { Location = "New Location" };

        RacecourseResponse result = await service.UpdateRacecourseAsync(racecourse.Id, request);

        Assert.Equal("Old Name", result.RacecourseName);
        Assert.Equal("New Location", result.Location);
        Assert.Equal("Dirt", result.TrackType);
    }

    [Fact]
    public async Task DeleteRacecourseAsync_SoftDeletes()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "To Delete" };
        db.Racecourses.Add(racecourse);
        await db.SaveChangesAsync();

        RacecourseService service = CreateService(db);

        bool result = await service.DeleteRacecourseAsync(racecourse.Id);

        Assert.True(result);
        Racecourse updated = await db.Racecourses.AsNoTracking().SingleAsync(r => r.Id == racecourse.Id);
        Assert.True(updated.IsDeleted);
        Assert.NotNull(updated.DeletedAt);
    }

    [Fact]
    public async Task DeleteRacecourseAsync_AlreadyDeleted_ThrowsKeyNotFound()
    {
        using HorseRacingDataContext db = CreateContext();
        var racecourse = new Racecourse { Id = Guid.NewGuid(), RacecourseName = "Gone", IsDeleted = true };
        db.Racecourses.Add(racecourse);
        await db.SaveChangesAsync();

        RacecourseService service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteRacecourseAsync(racecourse.Id));
    }

    [Fact]
    public async Task GetAllRacecoursesPagingAsync_ClampsInvalidPageAndPageSize()
    {
        using HorseRacingDataContext db = CreateContext();
        for (int i = 0; i < 3; i++)
            db.Racecourses.Add(new Racecourse { Id = Guid.NewGuid(), RacecourseName = $"Track{i}" });
        await db.SaveChangesAsync();

        RacecourseService service = CreateService(db);

        PagedResponse<RacecourseResponse> result = await service.GetAllRacecoursesPagingAsync(page: -1, pageSize: 1000);

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }
}
