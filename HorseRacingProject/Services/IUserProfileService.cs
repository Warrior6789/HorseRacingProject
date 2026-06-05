using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IUserProfileService
    {
        Task<UserProfileResponse> CreateUserProfileAsync(Guid accountId, UserProfileCreateRequest req);
        Task<UserProfileResponse> GetUserProfileByIdAsync(Guid accountId);
        Task<List<UserProfileResponse>> GetAllUserProfilesAsync();
        Task UpdateUserProfileAsync(Guid accountId, UserProfileUpdateRequest req);
    }
}
