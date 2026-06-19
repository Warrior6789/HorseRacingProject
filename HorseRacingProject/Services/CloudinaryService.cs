using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace HorseRacingAPI.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        public CloudinaryService(IConfiguration config)
        {
            Account acc = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
                );
            _cloudinary = new Cloudinary(acc);
            _cloudinary.Api.Secure = true;
        }
        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        public async Task<string> UploadImageAsync(IFormFile file, string folder)
        {
            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException("Image file size must not exceed 5 MB.");

            string ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
                throw new InvalidOperationException("Only JPG, JPEG, PNG, and WEBP images are allowed.");

            using Stream stream = file.OpenReadStream();
            ImageUploadParams uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            };
            ImageUploadResult result = await _cloudinary.UploadAsync(uploadParams);
            if (result.Error != null)
                throw new InvalidOperationException($"Image upload failed: {result.Error.Message}");
            return result.SecureUrl.ToString();
        }

        public async Task DeleteImageAsync(string publicId)
        {
            DeletionParams deletionParams = new DeletionParams(publicId);
            await _cloudinary.DestroyAsync(deletionParams);
        }
    }
}
