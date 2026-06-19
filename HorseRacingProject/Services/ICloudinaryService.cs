namespace HorseRacingAPI.Services
{
    public interface ICloudinaryService
    {
        Task<string> UploadImageAsync(IFormFile file, string folder);
        Task DeleteImageAsync(string publicId);
    }
}
