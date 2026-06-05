using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class JockeyProfileService : IJockeyProfileService
    {
        private readonly IUnitofWork _uow;

        public JockeyProfileService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<JockeyProfileResponse> CreateJockeyProfileAsync(JockeyProfileCreateRequest req)
        {
            if (req.AccountId == Guid.Empty)
                throw new InvalidOperationException("AccountId is required.");

            IGenericRepository<Account> accRepo = _uow.GetRepository<Account>();
            Account? account = await accRepo.Entities
                .FirstOrDefaultAsync(a => a.Id == req.AccountId && !a.IsDeleted);
            if (account == null)
                throw new InvalidOperationException("Account does not exist.");

            if (account.Role != AccountRole.Jockey)
                throw new InvalidOperationException("Only accounts with role Jockey can have a JockeyProfile.");

            IGenericRepository<JockeyProfile> jockeyProfileRepo = _uow.GetRepository<JockeyProfile>();
            Guid accountId = req.AccountId;
            int existingCount = await jockeyProfileRepo.Entities.CountAsync(p => p.AccountId == accountId);
            if (existingCount > 0)
                throw new InvalidOperationException("A jockey profile already exists for this account.");

            JockeyProfile jockeyProfile = new JockeyProfile
            {
                JockeyProfileId = Guid.NewGuid(),
                AccountId = req.AccountId,
                ExperienceYears = req.ExperienceYears,
                JockeyRating = req.JockeyRating,
                CreateAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };
            await jockeyProfileRepo.AddAsync(jockeyProfile);
            await _uow.SaveAsync();

            return new JockeyProfileResponse
            {
                JockeyProfileId = jockeyProfile.JockeyProfileId,
                AccountId = jockeyProfile.AccountId,
                ExperienceYears = jockeyProfile.ExperienceYears,
                JockeyRating = jockeyProfile.JockeyRating,
                CreateAt = jockeyProfile.CreateAt,
                UpdatedAt = jockeyProfile.UpdatedAt
            };
        }

        public async Task<List<JockeyProfileResponse>> GetAllJockeyProfilesAsync()
        {
            IGenericRepository<JockeyProfile> jockeyProfileRepo = _uow.GetRepository<JockeyProfile>();
            return await jockeyProfileRepo.Entities
                .Where(p => !p.IsDeleted)
                .Select(p => new JockeyProfileResponse
                {
                    JockeyProfileId = p.JockeyProfileId,
                    AccountId = p.AccountId,
                    ExperienceYears = p.ExperienceYears,
                    JockeyRating = p.JockeyRating,
                    CreateAt = p.CreateAt,
                    UpdatedAt = p.UpdatedAt
                }).ToListAsync();
        }

        public async Task<JockeyProfileResponse> GetJockeyProfileByAccountIdAsync(Guid accountId)
        {
            if (accountId == Guid.Empty)
                throw new InvalidOperationException("AccountId is required.");

            IGenericRepository<JockeyProfile> jockeyProfileRepo = _uow.GetRepository<JockeyProfile>();
            JockeyProfileResponse? response = await jockeyProfileRepo.Entities
                .Where(p => p.AccountId == accountId && !p.IsDeleted)
                .Select(p => new JockeyProfileResponse
                {
                    JockeyProfileId = p.JockeyProfileId,
                    AccountId = p.AccountId,
                    ExperienceYears = p.ExperienceYears,
                    JockeyRating = p.JockeyRating,
                    CreateAt = p.CreateAt,
                    UpdatedAt = p.UpdatedAt
                }).FirstOrDefaultAsync();

            if (response == null)
                throw new InvalidOperationException("JockeyProfile not found.");

            return response;
        }

        public async Task UpdateJockeyProfileAsync(Guid accountId, JockeyProfileUpdateRequest req)
        {
            if (accountId == Guid.Empty)
                throw new InvalidOperationException("AccountId is required.");

            IGenericRepository<JockeyProfile> jockeyProfileRepo = _uow.GetRepository<JockeyProfile>();
            JockeyProfile? jockeyProfile = await jockeyProfileRepo.Entities
                .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);
            if (jockeyProfile == null)
                throw new InvalidOperationException("JockeyProfile not found.");

            if (req.ExperienceYears.HasValue)
                jockeyProfile.ExperienceYears = req.ExperienceYears;

            if (req.JockeyRating.HasValue)
                jockeyProfile.JockeyRating = req.JockeyRating;

            jockeyProfile.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }
    }
}
