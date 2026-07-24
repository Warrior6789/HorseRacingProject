using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IUserProfileService
    {
        Task<UserProfileResponse> GetUserProfileByIdAsync(Guid accountId);
        Task UpdateUserProfileAsync(Guid accountId, UserProfileUpdateRequest req);
        Task<string> UploadImageAsync(Guid accountId, IFormFile file);

    }
}
