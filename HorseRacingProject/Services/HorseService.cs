using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Helpers;
using HorseRacingAPI.Middlewares;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class HorseService : IHorseService
    {
        private readonly IUnitofWork _uow;
        private readonly ICloudinaryService _cloudinaryService;

        public HorseService(IUnitofWork uow, ICloudinaryService cloudinaryService)
        {
            _uow = uow;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<PagedResponse<HorseDetailResponse>> GetHorsesAsync(Guid accountId, bool isAdmin, HorseQueryRequest query)
        {
            NormalizePaging(query);

            IQueryable<Horse> horses = BuildHorseScope(accountId, isAdmin);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                string search = query.Search.Trim().ToLower();
                horses = horses.Where(h =>
                    h.HorseName.ToLower().Contains(search) ||
                    (h.Breed != null && h.Breed.ToLower().Contains(search)) ||
                    (h.Color != null && h.Color.ToLower().Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(query.Status) &&
                Enum.TryParse<HorseStatus>(query.Status.Trim(), ignoreCase: true, out var statusFilter))
            {
                horses = horses.Where(h => h.Status == statusFilter);
            }

            if (!string.IsNullOrWhiteSpace(query.Breed))
            {
                string breed = query.Breed.Trim().ToLower();
                horses = horses.Where(h => h.Breed != null && h.Breed.ToLower().Contains(breed));
            }

            if (!string.IsNullOrWhiteSpace(query.Color))
            {
                string color = query.Color.Trim().ToLower();
                horses = horses.Where(h => h.Color != null && h.Color.ToLower().Contains(color));
            }

            int totalCount = await horses.CountAsync();

            RaceStatus[] upcomingRaceStatuses =
            [
                RaceStatus.Scheduled,
                RaceStatus.BettingOpen,
                RaceStatus.BettingClosed
            ];

            List<HorseDetailResponse> items = await horses
                .OrderBy(h => h.HorseName)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(h => new HorseDetailResponse
                {
                    Id = h.Id,
                    OwnerId = h.OwnerId,
                    HorseName = h.HorseName,
                    Age = h.Age,
                    Breed = h.Breed,
                    Weight = h.Weight,
                    Status = h.Status.ToString(),
                    DerivedStatus = h.Registrations.Any(r => r.Status == RegistrationStatus.Confirmed && !r.Race.IsDeleted && r.Race.Status == RaceStatus.Live)
                        ? "Racing"
                        : h.Registrations.Any(r => r.Status == RegistrationStatus.Confirmed && !r.Race.IsDeleted && upcomingRaceStatuses.Contains(r.Race.Status))
                            ? "Registered"
                            : h.Status.ToString(),
                    RecordWins = h.RecordWins,
                    Color = h.Color,
                    ImageUrl = h.ImageUrl,
                    CreateAt = h.CreateAt,
                    UpdatedAt = h.UpdatedAt
                })
                .ToListAsync();

            return new PagedResponse<HorseDetailResponse>
            {
                Items = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<HorseDetailResponse> GetHorseByIdAsync(Guid horseId, Guid accountId, bool isAdmin)
        {
            Horse horse = await GetHorseEntityAsync(horseId, accountId, isAdmin);
            return MapToResponse(horse);
        }

        public async Task<HorseDetailResponse> CreateHorseAsync(Guid accountId, bool isAdmin, HorseCreateRequest request)
        {
            Guid ownerId = isAdmin ? request.OwnerId ?? Guid.Empty : accountId;
            if (ownerId == Guid.Empty)
                throw new InvalidOperationException("OwnerId is required when Admin creates a horse.");

            await ValidateOwnerAsync(ownerId);

            if (request.Image != null)
                request.ImageUrl = await _cloudinaryService.UploadImageAsync(request.Image, "horses");

            IGenericRepository<Horse> horseRepo = _uow.GetRepository<Horse>();
            Horse horse = new Horse
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                HorseName = request.HorseName,
                Age = request.Age,
                Breed = request.Breed,
                Weight = request.Weight,
                Status = HorseStatus.Healthy,
                RecordWins = request.RecordWins ?? 0,
                Color = request.Color,
                ImageUrl = request.ImageUrl,
                CreateAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };

            try
            {
                await horseRepo.AddAsync(horse);
                await _uow.SaveAsync();
            }
            catch
            {
                if (request.ImageUrl != null)
                {
                    string publicId = Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out Uri? uri)
                        ? string.Join("/", uri.AbsolutePath.TrimStart('/').Split('/').TakeLast(2)).Split('.')[0]
                        : request.ImageUrl;
                    await _cloudinaryService.DeleteImageAsync(publicId);
                }
                throw;
            }

            return MapToResponse(horse);
        }

        public async Task<HorseDetailResponse> UpdateHorseAsync(Guid horseId, Guid accountId, bool isAdmin, HorseUpdateRequest request)
        {
            Horse horse = await GetHorseEntityAsync(horseId, accountId, isAdmin);

            if (!string.IsNullOrWhiteSpace(request.HorseName))
                horse.HorseName = request.HorseName;

            if (request.Age.HasValue)
                horse.Age = request.Age;

            if (!string.IsNullOrWhiteSpace(request.Breed))
                horse.Breed = request.Breed;

            if (request.Weight.HasValue)
                horse.Weight = request.Weight;

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                HorseStatus newStatus = HorseStatusPolicy.NormalizeRequired(request.Status);
                if (newStatus != HorseStatus.Healthy)
                {
                    bool hasActiveRace = horse.Registrations.Any(r =>
                        (r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending)
                        && r.Race.Status != RaceStatus.Finished
                        && r.Race.Status != RaceStatus.Completed
                        && r.Race.Status != RaceStatus.Cancelled);
                    if (hasActiveRace)
                        throw new InvalidOperationException($"Cannot mark horse as {newStatus} while it has an active race registration. Contact an admin to scratch it from the race first.");
                }
                horse.Status = newStatus;
            }

            if (request.RecordWins.HasValue)
                horse.RecordWins = request.RecordWins;

            if (!string.IsNullOrWhiteSpace(request.Color))
                horse.Color = request.Color;

            horse.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();

            return MapToResponse(horse);
        }

        public async Task DeleteHorseAsync(Guid horseId, Guid accountId, bool isAdmin)
        {
            Horse horse = await GetHorseEntityAsync(horseId, accountId, isAdmin);

            bool hasActiveRace = horse.Registrations.Any(r =>
                (r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending)
                && r.Race.Status != RaceStatus.Finished
                && r.Race.Status != RaceStatus.Completed
                && r.Race.Status != RaceStatus.Cancelled);
            if (hasActiveRace)
                throw new InvalidOperationException("Cannot delete a horse with an active or upcoming race registration.");

            horse.IsDeleted = true;
            horse.DeletedAt = DateTimeOffset.UtcNow;
            horse.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }

        public async Task<PagedResponse<HorseResponse>> GetActiveHorsesPagedAsync(Guid accountId, bool isAdmin, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            RaceStatus[] upcomingRaceStatuses =
            [
                RaceStatus.Scheduled,
                RaceStatus.BettingOpen,
                RaceStatus.BettingClosed
            ];

            IQueryable<Horse> query = BuildHorseScope(accountId, isAdmin)
                .Where(h => HorseStatusPolicy.ActiveStatuses.Contains(h.Status))
                .OrderBy(h => h.HorseName);

            int total = await query.CountAsync();

            List<HorseResponse> items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(h => new HorseResponse
                {
                    Id         = h.Id,
                    HorseName  = h.HorseName,
                    Age        = h.Age,
                    Breed      = h.Breed,
                    Weight     = h.Weight,
                    Status     = h.Status.ToString(),
                    DerivedStatus = h.Registrations.Any(r => r.Status == RegistrationStatus.Confirmed && !r.Race.IsDeleted && r.Race.Status == RaceStatus.Live)
                        ? "Racing"
                        : h.Registrations.Any(r => r.Status == RegistrationStatus.Confirmed && !r.Race.IsDeleted && upcomingRaceStatuses.Contains(r.Race.Status))
                            ? "Registered"
                            : h.Status.ToString(),
                    RecordWins = h.RecordWins,
                    Color      = h.Color,
                    ImageUrl   = h.ImageUrl
                })
                .ToListAsync();

            return new PagedResponse<HorseResponse>
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = total
            };
        }

        public async Task<string> UploadImageAsync(Guid horseId, Guid accountId, bool isAdmin, IFormFile file)
        {
            Horse horse = await GetHorseEntityAsync(horseId, accountId, isAdmin);

            string? oldImageUrl = horse.ImageUrl;

            string newImageUrl = await _cloudinaryService.UploadImageAsync(file, "horses");

            try
            {
                horse.ImageUrl = newImageUrl;
                horse.UpdatedAt = DateTimeOffset.UtcNow;
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

        private IQueryable<Horse> BuildHorseScope(Guid accountId, bool isAdmin)
        {
            IQueryable<Horse> query = _uow.GetRepository<Horse>().Entities
                .Where(h => !h.IsDeleted);

            if (!isAdmin)
                query = query.Where(h => h.OwnerId == accountId);

            return query;
        }

        public async Task<HorsePerformanceSummaryResponse> GetPerformanceSummaryAsync(Guid horseId, Guid accountId, bool isAdmin)
        {
            Horse horse = await GetHorseEntityAsync(horseId, accountId, isAdmin);

            IGenericRepository<Registration> regRepo = _uow.GetRepository<Registration>();
            IGenericRepository<Prize> prizeRepo = _uow.GetRepository<Prize>();

            int totalRaces = await regRepo.Entities
                .CountAsync(r => r.HorseId == horseId && r.Status == RegistrationStatus.Confirmed);

            int totalWins = await _uow.GetRepository<RaceResult>().Entities
                .CountAsync(rr => rr.Registration.HorseId == horseId
                    && rr.FinishPosition == 1
                    && rr.IsDisqualified != true);

            decimal totalEarned = await prizeRepo.Entities
                .Where(p => p.Registration.HorseId == horseId && p.PrizeType == PrizeType.Owner)
                .SumAsync(p => p.Amount ?? 0);

            return new HorsePerformanceSummaryResponse
            {
                HorseId = horse.Id,
                HorseName = horse.HorseName,
                TotalRaces = totalRaces,
                TotalWins = totalWins,
                TotalEarned = totalEarned
            };
        }

        public async Task<PagedResponse<OwnerRaceHistoryItemResponse>> GetOwnerRaceHistoryAsync(Guid ownerId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IQueryable<Prize> query = _uow.GetRepository<Prize>().Entities
                .Where(p => p.PrizeType == PrizeType.Owner && p.Registration.Horse.OwnerId == ownerId);

            int totalCount = await query.CountAsync();

            List<OwnerRaceHistoryItemResponse> items = await query
                .OrderByDescending(p => p.DistributedAt ?? DateTimeOffset.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new OwnerRaceHistoryItemResponse
                {
                    RegistrationId = p.RegistrationId,
                    RaceId = p.Registration.RaceId,
                    Date = p.Registration.Race.StartTime,
                    RaceName = p.Registration.Race.RaceName,
                    RaceNumber = p.Registration.Race.RaceNumber,
                    HorseId = p.Registration.HorseId,
                    HorseName = p.Registration.Horse.HorseName,
                    HorseImageUrl = p.Registration.Horse.ImageUrl,
                    JockeyName = p.Registration.Jockey.JockeyProfile != null ? p.Registration.Jockey.JockeyProfile.FullName : null,
                    RacecourseName = p.Registration.Race.Racecourse.RacecourseName,
                    TrackType = p.Registration.Race.Racecourse.TrackType,
                    Position = p.Registration.RaceResult != null ? p.Registration.RaceResult.FinishPosition : null,
                    Earnings = p.Amount
                })
                .ToListAsync();

            return new PagedResponse<OwnerRaceHistoryItemResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        private async Task<Horse> GetHorseEntityAsync(Guid horseId, Guid accountId, bool isAdmin)
        {
            Horse? horse = await _uow.GetRepository<Horse>().Entities
                .Include(h => h.Registrations)
                    .ThenInclude(r => r.Race)
                .FirstOrDefaultAsync(h => h.Id == horseId && !h.IsDeleted);

            if (horse == null)
                throw new KeyNotFoundException($"Horse with id {horseId} not found.");

            if (!isAdmin && horse.OwnerId != accountId)
                throw new ForbiddenAccessException("You do not have permission to access this horse.");

            return horse;
        }

        private async Task ValidateOwnerAsync(Guid ownerId)
        {
            Account? owner = await _uow.GetRepository<Account>().Entities
                .FirstOrDefaultAsync(a => a.Id == ownerId && !a.IsDeleted);

            if (owner == null)
                throw new KeyNotFoundException($"Owner account with id {ownerId} not found.");

            if (owner.Role != AccountRole.HorseOwner)
                throw new InvalidOperationException("Only HorseOwner accounts can own horses.");
        }

        private static void NormalizePaging(HorseQueryRequest query)
        {
            if (query.Page < 1)
                query.Page = 1;

            if (query.PageSize < 1)
                query.PageSize = 10;

            if (query.PageSize > 100)
                query.PageSize = 100;
        }

        private static HorseDetailResponse MapToResponse(Horse horse) => new HorseDetailResponse
        {
            Id = horse.Id,
            OwnerId = horse.OwnerId,
            HorseName = horse.HorseName,
            Age = horse.Age,
            Breed = horse.Breed,
            Weight = horse.Weight,
            Status = horse.Status.ToString(),
            DerivedStatus = GetDerivedStatus(horse),
            RecordWins = horse.RecordWins,
            Color = horse.Color,
            ImageUrl = horse.ImageUrl,
            CreateAt = horse.CreateAt,
            UpdatedAt = horse.UpdatedAt
        };

        private static string? GetDerivedStatus(Horse horse)
        {
            bool hasLiveRace = horse.Registrations.Any(r =>
                r.Status == RegistrationStatus.Confirmed &&
                !r.Race.IsDeleted &&
                r.Race.Status == RaceStatus.Live);

            if (hasLiveRace)
                return "Racing";

            RaceStatus[] upcomingRaceStatuses =
            [
                RaceStatus.Scheduled,
                RaceStatus.BettingOpen,
                RaceStatus.BettingClosed
            ];

            bool hasUpcomingRace = horse.Registrations.Any(r =>
                r.Status == RegistrationStatus.Confirmed &&
                !r.Race.IsDeleted &&
                upcomingRaceStatuses.Contains(r.Race.Status));

            if (hasUpcomingRace)
                return "Registered";

            return horse.Status.ToString();
        }
    }
}
