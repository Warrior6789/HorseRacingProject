using HorseRacingAPI.Dtos;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IUnitofWork _uow;
        private readonly ICloudinaryService _cloudinaryService;
        public UserProfileService(IUnitofWork uow, ICloudinaryService cloudinaryService)
        {
            _uow = uow;
            _cloudinaryService = cloudinaryService;
        }
        public async Task<UserProfileResponse> GetUserProfileByIdAsync(Guid accountId)
        {
            IGenericRepository<UserProfile> userProfileRepo = _uow.GetRepository<UserProfile>();
            if (accountId == Guid.Empty)
            {
                throw new Exception("AccountId is required.");
            }
            UserProfileResponse? response = await userProfileRepo.Entities
                .Where(u => u.AccountId == accountId)
                .Select(u => new UserProfileResponse
                {
                    ProfileId = u.ProfileId,
                    AccountId = u.AccountId,
                    FullName = u.FullName,
                    Phone = u.Phone,
                    Balance = u.Balance,
                    ImageUrl = u.ImageUrl,
                    CreateAt = u.CreateAt,
                    UpdatedAt = u.UpdatedAt,
                }).FirstOrDefaultAsync();

            if (response == null)
            {
                throw new Exception("UserProfile not found.");
            }
            return response;
        }

        public async Task UpdateUserProfileAsync(Guid accountId, UserProfileUpdateRequest req)
        {
            IGenericRepository<UserProfile> userProfileRepo = _uow.GetRepository<UserProfile>();
            if (accountId == Guid.Empty)
            {
                throw new Exception("AccountId is required.");
            }
            UserProfile? userProfile = await userProfileRepo.Entities
            .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);
            if (userProfile == null)
            {
                throw new Exception("User profile not found."); 
            }
            if (!string.IsNullOrEmpty(req.FullName))
            {
                userProfile.FullName = req.FullName;
            }

            if (!string.IsNullOrEmpty(req.Phone))
            {
                userProfile.Phone = req.Phone;
            }

            userProfile.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }

        public async Task<string> UploadImageAsync(Guid accountId, IFormFile file)
        {
            IGenericRepository<UserProfile> userProfileRepo = _uow.GetRepository<UserProfile>();
            UserProfile? userProfile = await userProfileRepo.Entities.FirstOrDefaultAsync(u => u.AccountId == accountId && !u.IsDeleted);
            if (userProfile == null)
            {
                throw new KeyNotFoundException("User profile not found.");
            }
            string imageUrl = await _cloudinaryService.UploadImageAsync(file, "user-profiles");
            userProfile.ImageUrl = imageUrl;
            userProfile.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
            return imageUrl;
        }
    }
}
