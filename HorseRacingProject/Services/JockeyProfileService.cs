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
        private readonly ICloudinaryService _cloudinaryService;

        public JockeyProfileService(IUnitofWork uow, ICloudinaryService cloudinaryService)
        {
            _uow = uow;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<JockeyProfileResponse> CreateJockeyProfileAsync(Guid accountId, JockeyProfileCreateRequest req)
        {
            IGenericRepository<Account> accRepo = _uow.GetRepository<Account>();
            Account? account = await accRepo.Entities
                .FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted);
            if (account == null)
                throw new InvalidOperationException("Account does not exist.");

            if (account.Role != AccountRole.Jockey)
                throw new InvalidOperationException("Only accounts with role Jockey can have a JockeyProfile.");

            IGenericRepository<JockeyProfile> repo = _uow.GetRepository<JockeyProfile>();
            int existingCount = await repo.Entities.CountAsync(p => p.AccountId == accountId);
            if (existingCount > 0)
                throw new InvalidOperationException("A jockey profile already exists for this account.");

            JockeyProfile jockeyProfile = new JockeyProfile
            {
                JockeyProfileId = Guid.NewGuid(),
                AccountId = accountId,
                FullName = req.FullName,
                DateOfBirth = req.DateOfBirth,
                Nationality = req.Nationality,
                LicenseNumber = req.LicenseNumber,
                TotalRaces = 0,
                TotalWins = 0,
                CreateAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };
            await repo.AddAsync(jockeyProfile);
            await _uow.SaveAsync();

            return MapToResponse(jockeyProfile);
        }

        public async Task<List<JockeyProfileResponse>> GetAllJockeyProfilesAsync()
        {
            IGenericRepository<JockeyProfile> repo = _uow.GetRepository<JockeyProfile>();
            return await repo.Entities
                .Where(p => !p.IsDeleted)
                .Select(p => new JockeyProfileResponse
                {
                    JockeyProfileId = p.JockeyProfileId,
                    AccountId = p.AccountId,
                    FullName = p.FullName,
                    DateOfBirth = p.DateOfBirth,
                    Nationality = p.Nationality,
                    LicenseNumber = p.LicenseNumber,
                    TotalRaces = p.TotalRaces,
                    TotalWins = p.TotalWins,
                    ImageUrl = p.ImageUrl,
                    CreateAt = p.CreateAt,
                    UpdatedAt = p.UpdatedAt
                }).ToListAsync();
        }

        public async Task<JockeyProfileResponse> GetJockeyProfileByAccountIdAsync(Guid accountId)
        {
            if (accountId == Guid.Empty)
                throw new InvalidOperationException("AccountId is required.");

            IGenericRepository<JockeyProfile> repo = _uow.GetRepository<JockeyProfile>();
            JockeyProfileResponse? response = await repo.Entities
                .Where(p => p.AccountId == accountId && !p.IsDeleted)
                .Select(p => new JockeyProfileResponse
                {
                    JockeyProfileId = p.JockeyProfileId,
                    AccountId = p.AccountId,
                    FullName = p.FullName,
                    DateOfBirth = p.DateOfBirth,
                    Nationality = p.Nationality,
                    LicenseNumber = p.LicenseNumber,
                    TotalRaces = p.TotalRaces,
                    TotalWins = p.TotalWins,
                    ImageUrl = p.ImageUrl,
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

            IGenericRepository<JockeyProfile> repo = _uow.GetRepository<JockeyProfile>();
            JockeyProfile? jockeyProfile = await repo.Entities
                .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);
            if (jockeyProfile == null)
                throw new InvalidOperationException("JockeyProfile not found.");

            if (!string.IsNullOrEmpty(req.FullName))
                jockeyProfile.FullName = req.FullName;

            if (req.DateOfBirth.HasValue)
                jockeyProfile.DateOfBirth = req.DateOfBirth;

            if (!string.IsNullOrEmpty(req.Nationality))
                jockeyProfile.Nationality = req.Nationality;

            if (!string.IsNullOrEmpty(req.LicenseNumber))
                jockeyProfile.LicenseNumber = req.LicenseNumber;

            jockeyProfile.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }

        private static JockeyProfileResponse MapToResponse(JockeyProfile p) => new JockeyProfileResponse
        {
            JockeyProfileId = p.JockeyProfileId,
            AccountId = p.AccountId,
            FullName = p.FullName,
            DateOfBirth = p.DateOfBirth,
            Nationality = p.Nationality,
            LicenseNumber = p.LicenseNumber,
            TotalRaces = p.TotalRaces,
            TotalWins = p.TotalWins,
            ImageUrl = p.ImageUrl,
            CreateAt = p.CreateAt,
            UpdatedAt = p.UpdatedAt
        };

        public async Task<string> UploadImageAsync(Guid accountId, IFormFile file)
        {
            IGenericRepository<JockeyProfile> jockeyProfileRepo = _uow.GetRepository<JockeyProfile>();
            JockeyProfile? profile = await jockeyProfileRepo.Entities.FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);
            if (profile == null)
            {
                throw new KeyNotFoundException("JockeyProfile not found.");
            }
            string imageUrl = await _cloudinaryService.UploadImageAsync(file, "jockey-profiles");
            profile.ImageUrl = imageUrl;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
            return imageUrl;
        }
    }
}
