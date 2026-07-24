
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
                    Weight = p.Weight,
                    Height = p.Height,
                    TotalRaces = p.TotalRaces,
                    TotalWins = p.TotalWins,
                    Balance = p.Balance,
                    ImageUrl = p.ImageUrl,
                    CertificateImageUrl = p.CertificateImageUrl,
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
                    Weight = p.Weight,
                    Height = p.Height,
                    TotalRaces = p.TotalRaces,
                    TotalWins = p.TotalWins,
                    Balance = p.Balance,
                    ImageUrl = p.ImageUrl,
                    CertificateImageUrl = p.CertificateImageUrl,
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

            if (req.Weight.HasValue)
                jockeyProfile.Weight = req.Weight;

            if (req.Height.HasValue)
                jockeyProfile.Height = req.Height;

            jockeyProfile.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }

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

        public async Task<JockeyRewardsResponse> GetJockeyRewardsAsync(Guid accountId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            JockeyProfile? profile = await _uow.GetRepository<JockeyProfile>().Entities
                .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);
            if (profile == null)
                throw new KeyNotFoundException("Jockey profile not found.");

            IQueryable<Prize> query = _uow.GetRepository<Prize>().Entities
                .Where(p => p.PrizeType == PrizeType.Jockey
                    && p.Registration.JockeyId == accountId
                    && !p.Registration.Race.IsDeleted);

            int totalCount = await query.CountAsync();
            decimal totalAmount = await query.SumAsync(p => p.Amount ?? 0);

            List<HorseRewardItemResponse> items = await query
                .OrderByDescending(p => p.DistributedAt ?? DateTimeOffset.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new HorseRewardItemResponse
                {
                    PrizeId = p.PrizeId,
                    RegistrationId = p.RegistrationId,
                    RaceId = p.Registration.RaceId,
                    RaceNumber = p.Registration.Race.RaceNumber,
                    PrizeType = p.PrizeType.ToString(),
                    Amount = p.Amount,
                    DistributedAt = p.DistributedAt
                })
                .ToListAsync();

            return new JockeyRewardsResponse
            {
                JockeyAccountId = accountId,
                FullName = profile.FullName ?? string.Empty,
                TotalRewardAmount = totalAmount,
                RewardCount = totalCount,
                Rewards = new PagedResponse<HorseRewardItemResponse>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                }
            };
        }

        public async Task<PagedResponse<JockeyRaceHistoryItemResponse>> GetJockeyRaceHistoryAsync(Guid accountId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IQueryable<Registration> query = _uow.GetRepository<Registration>().Entities
                .Where(r => r.JockeyId == accountId
                    && r.Status == RegistrationStatus.Confirmed
                    && r.Race.Status == RaceStatus.Finished
                    && !r.Race.IsDeleted);

            int totalCount = await query.CountAsync();

            List<JockeyRaceHistoryItemResponse> items = await query
                .OrderByDescending(r => r.Race.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new JockeyRaceHistoryItemResponse
                {
                    RegistrationId = r.RegistrationId,
                    RaceId = r.RaceId,
                    Date = r.Race.StartTime,
                    RaceName = r.Race.RaceName,
                    RaceNumber = r.Race.RaceNumber,
                    HorseName = r.Horse.HorseName,
                    HorseImageUrl = r.Horse.ImageUrl,
                    RacecourseName = r.Race.Racecourse.RacecourseName,
                    TrackType = r.Race.Racecourse.TrackType,
                    Position = r.RaceResult != null ? r.RaceResult.FinishPosition : null,
                    Earnings = r.Prizes.Where(p => p.PrizeType == PrizeType.Jockey).Select(p => (decimal?)p.Amount).FirstOrDefault()
                })
                .ToListAsync();

            return new PagedResponse<JockeyRaceHistoryItemResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
    }
}
