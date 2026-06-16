using Microsoft.AspNetCore.Http;

namespace HorseRacingAPI.Services
{
    public class LocalHorseImageStorageService : IHorseImageStorageService
    {
        private const long MaxImageSizeInBytes = 5 * 1024 * 1024;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private readonly IWebHostEnvironment _environment;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LocalHorseImageStorageService(IWebHostEnvironment environment, IHttpContextAccessor httpContextAccessor)
        {
            _environment = environment;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> SaveImageAsync(IFormFile image)
        {
            if (image == null || image.Length == 0)
                throw new InvalidOperationException("Image file is required.");

            if (image.Length > MaxImageSizeInBytes)
                throw new InvalidOperationException("Image file size must be 5MB or smaller.");

            string extension = Path.GetExtension(image.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("Only JPG, JPEG, PNG, and WEBP images are allowed.");

            string webRootPath = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");

            string uploadDirectory = Path.Combine(webRootPath, "uploads", "horses");
            Directory.CreateDirectory(uploadDirectory);

            string fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            string filePath = Path.Combine(uploadDirectory, fileName);

            await using FileStream stream = File.Create(filePath);
            await image.CopyToAsync(stream);

            HttpRequest? request = _httpContextAccessor.HttpContext?.Request;
            if (request == null)
                return $"/uploads/horses/{fileName}";

            return $"{request.Scheme}://{request.Host}/uploads/horses/{fileName}";
        }
    }
}
