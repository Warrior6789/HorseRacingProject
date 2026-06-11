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
        public async Task<string> UploadImageAsync(IFormFile file, string folder)
        {

            using Stream stream = file.OpenReadStream();
            ImageUploadParams uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            };
            ImageUploadResult result = await _cloudinary.UploadAsync(uploadParams);
            if(result.Error != null){
                throw new InvalidOperationException($"Image upload failed: {result.Error.Message}");
            }
            return result.SecureUrl.ToString();
        }
    }
}
