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
        public async Task<UserProfileResponse> CreateUserProfileAsync(Guid accountId, UserProfileCreateRequest req)
        {
            IGenericRepository<UserProfile> userProfileRepo = _uow.GetRepository<UserProfile>();

            IGenericRepository<Account> accRepo = _uow.GetRepository<Account>();
            bool accountExsit = await accRepo.Entities.AnyAsync(a => a.Id == accountId && !a.IsDeleted);
            if (!accountExsit)
            {
                throw new Exception("Account does not exist.");
            }
            int existingCount = await userProfileRepo.Entities.CountAsync(p => p.AccountId == accountId);
            if (existingCount > 0)
            {
                throw new InvalidOperationException("A profile already exists for this account.");
            }
            string? imageUrl = null;
            if (req.Image != null)
                imageUrl = await _cloudinaryService.UploadImageAsync(req.Image, "user-profiles");

            UserProfile userProfile = new UserProfile
            {
                ProfileId = Guid.NewGuid(),
                AccountId = accountId,
                FullName = req.FullName,
                Phone = req.Phone,
                ImageUrl = imageUrl,
                Balance = 0,
                CreateAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };
            await userProfileRepo.AddAsync(userProfile);
            await _uow.SaveAsync();
            return new UserProfileResponse
            {
                ProfileId = userProfile.ProfileId,
                AccountId = userProfile.AccountId,
                FullName = userProfile.FullName,
                Phone = userProfile.Phone,
                Balance = userProfile.Balance,
                CreateAt = userProfile.CreateAt,
                UpdatedAt = userProfile.UpdatedAt
            };
        }

        public async Task<List<UserProfileResponse>> GetAllUserProfilesAsync()
        {
            IGenericRepository<UserProfile> userProfileRepo = _uow.GetRepository<UserProfile>();
            return await userProfileRepo.Entities.Select(u => new UserProfileResponse
            {
                ProfileId = u.ProfileId,
                AccountId = u.AccountId,
                FullName = u.FullName,
                Phone = u.Phone,
                Balance = u.Balance,
                ImageUrl = u.ImageUrl,
                CreateAt = u.CreateAt,
                UpdatedAt = u.UpdatedAt,
            }).ToListAsync();
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
