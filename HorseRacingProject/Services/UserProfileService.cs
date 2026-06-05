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
        public UserProfileService(IUnitofWork uow)
        {
            _uow = uow;
        }
        public async Task<UserProfileResponse> CreateUserProfileAsync(UserProfileCreateRequest req)
        {
            IGenericRepository<UserProfile> userProfileRepo = _uow.GetRepository<UserProfile>();

            if (req.AccountId == Guid.Empty)
            {
                throw new Exception("AccountId is required.");
            }
            IGenericRepository<Account> accRepo = _uow.GetRepository<Account>();
            bool accountExsit = await accRepo.Entities.AnyAsync(a => a.Id == req.AccountId && !a.IsDeleted);
            if(!accountExsit)
            {
                throw new Exception("Account does not exist.");
            }
            Guid accountId = req.AccountId;
            int existingCount = await userProfileRepo.Entities.CountAsync(p => p.AccountId == accountId);
            if (existingCount > 0)
            {
                throw new InvalidOperationException("A profile already exists for this account.");
            }
            UserProfile userProfile = new UserProfile
            {
                ProfileId = Guid.NewGuid(),
                AccountId = req.AccountId,
                FullName = req.FullName,
                Phone = req.Phone,
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
        }
    }
}
