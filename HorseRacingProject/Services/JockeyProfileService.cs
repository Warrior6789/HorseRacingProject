
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

            string? imageUrl = req.Image != null
                ? await _cloudinaryService.UploadImageAsync(req.Image, "jockey-profiles")
                : null;
            
            JockeyProfile jockeyProfile = new JockeyProfile
            {
                JockeyProfileId = Guid.NewGuid(),
                AccountId = accountId,
                FullName = req.FullName,
                DateOfBirth = req.DateOfBirth,
                Nationality = req.Nationality,
                LicenseNumber = req.LicenseNumber,
                ImageUrl = imageUrl,
                TotalRaces = 0,
                TotalWins = 0,
                CreateAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };
            try
            {
                await repo.AddAsync(jockeyProfile);
                await _uow.SaveAsync();
            }
            catch
            {
                if(imageUrl != null)
                {
                    string publicId = string.Join("/", new Uri(imageUrl).AbsolutePath
                   .TrimStart('/').Split('/').TakeLast(2)).Split('.')[0];
                    await _cloudinaryService.DeleteImageAsync(publicId);
                }
                throw;
            }
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

        public async Task<PagedResponse<JockeyProfileResponse>> GetAllJockeyProfilesPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            IGenericRepository<JockeyProfile> repo = _uow.GetRepository<JockeyProfile>();
            int totalCount = await repo.Entities.CountAsync(p => !p.IsDeleted);
            IEnumerable<JockeyProfileResponse> items = await repo.FindAsync<JockeyProfileResponse>(
                predicate: p => !p.IsDeleted,
                orderBy: q => q.OrderBy(p => p.CreateAt),
                selector: p => new JockeyProfileResponse
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
                },
                pageIndex: page - 1,
                pageSize: pageSize);
            return new PagedResponse<JockeyProfileResponse> { Items = items.ToList(), Page = page, PageSize = pageSize, TotalCount = totalCount };
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
            JockeyProfile? profile = await jockeyProfileRepo.Entities
                .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);
            if (profile == null)
                throw new KeyNotFoundException("JockeyProfile not found.");

            string? oldImageUrl = profile.ImageUrl;

            string newImageUrl = await _cloudinaryService.UploadImageAsync(file, "jockey-profiles");

            try
            {
                profile.ImageUrl = newImageUrl;
                profile.UpdatedAt = DateTimeOffset.UtcNow;
                await _uow.SaveAsync();
            }
            catch
            {
                string newPublicId = string.Join("/", new Uri(newImageUrl).AbsolutePath
                    .TrimStart('/').Split('/').TakeLast(2)).Split('.')[0];
                await _cloudinaryService.DeleteImageAsync(newPublicId);
                throw;
            }

            if (oldImageUrl != null)
            {
                string oldPublicId = string.Join("/", new Uri(oldImageUrl).AbsolutePath
                    .TrimStart('/').Split('/').TakeLast(2)).Split('.')[0];
                await _cloudinaryService.DeleteImageAsync(oldPublicId);
            }

            return newImageUrl;
        }
    }
}
