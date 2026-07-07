using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;

namespace HorseRacingProject.Tests;

public class CloudinaryServiceTests
{
    private static CloudinaryService CreateService()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Cloudinary:CloudName"] = "test-cloud",
            ["Cloudinary:ApiKey"] = "test-key",
            ["Cloudinary:ApiSecret"] = "test-secret"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        return new CloudinaryService(config);
    }

    private static IFormFile CreateFakeFile(long length, string fileName)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.FileName).Returns(fileName);
        return file.Object;
    }

    [Fact]
    public async Task UploadImageAsync_FileTooLarge_ThrowsInvalidOperationException()
    {
        CloudinaryService service = CreateService();
        IFormFile file = CreateFakeFile(length: 6 * 1024 * 1024, fileName: "photo.jpg");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UploadImageAsync(file, "horses"));

        Assert.Contains("5 MB", ex.Message);
    }

    [Theory]
    [InlineData("photo.gif")]
    [InlineData("photo.bmp")]
    [InlineData("photo")]
    public async Task UploadImageAsync_DisallowedExtension_ThrowsInvalidOperationException(string fileName)
    {
        CloudinaryService service = CreateService();
        IFormFile file = CreateFakeFile(length: 1024, fileName: fileName);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UploadImageAsync(file, "horses"));

        Assert.Contains("JPG, JPEG, PNG, and WEBP", ex.Message);
    }
}
