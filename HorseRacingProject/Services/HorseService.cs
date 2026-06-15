using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
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

        public HorseService(IUnitofWork uow)
        {
            _uow = uow;
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
                    (h.Color != null && h.Color.ToLower().Contains(search)) ||
                    (h.Status != null && h.Status.ToLower().Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                string status = query.Status.Trim().ToLower();
                horses = horses.Where(h => h.Status != null && h.Status.ToLower() == status);
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

            List<HorseDetailResponse> items = await horses
                .OrderBy(h => h.HorseName)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(h => MapToResponse(h))
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

            IGenericRepository<Horse> horseRepo = _uow.GetRepository<Horse>();
            Horse horse = new Horse
            {
                OwnerId = ownerId,
                HorseName = request.HorseName,
                Age = request.Age,
                Breed = request.Breed,
                Weight = request.Weight,
                Status = string.IsNullOrWhiteSpace(request.Status) ? HorseStatus.Healthy.ToString() : request.Status,
                RecordWins = request.RecordWins ?? 0,
                Color = request.Color,
                CreateAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };

            await horseRepo.AddAsync(horse);
            await _uow.SaveAsync();

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
                horse.Status = request.Status;

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
            horse.IsDeleted = true;
            horse.DeletedAt = DateTimeOffset.UtcNow;
            horse.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }

        public async Task<List<HorseResponse>> GetActiveHorsesAsync(Guid accountId, bool isAdmin)
        {
            return await BuildHorseScope(accountId, isAdmin)
                .Where(h => h.Status != null && h.Status.ToLower() == "active")
                .OrderBy(h => h.HorseName)
                .Select(h => new HorseResponse
                {
                    Id = h.Id,
                    HorseName = h.HorseName,
                    Age = h.Age,
                    Breed = h.Breed,
                    Weight = h.Weight,
                    Status = h.Status,
                    RecordWins = h.RecordWins,
                    Color = h.Color
                })
                .ToListAsync();
        }

        public async Task<List<HorseScheduleResponse>> GetMyScheduleAsync(Guid ownerId)
        {
            return await BuildScheduleQuery()
                .Where(r => r.Horse.OwnerId == ownerId)
                .OrderBy(r => r.Race.StartTime)
                .Select(r => MapToScheduleResponse(r))
                .ToListAsync();
        }

        public async Task<List<HorseScheduleResponse>> GetHorseScheduleAsync(Guid horseId, Guid accountId, bool isAdmin)
        {
            await GetHorseEntityAsync(horseId, accountId, isAdmin);

            return await BuildScheduleQuery()
                .Where(r => r.HorseId == horseId)
                .OrderBy(r => r.Race.StartTime)
                .Select(r => MapToScheduleResponse(r))
                .ToListAsync();
        }

        public async Task<HorseRewardsResponse> GetHorseRewardsAsync(Guid horseId, Guid accountId, bool isAdmin, HorseRewardsQueryRequest query)
        {
            NormalizePaging(query);

            Horse horse = await GetHorseEntityAsync(horseId, accountId, isAdmin);

            IQueryable<Prize> rewardQuery = _uow.GetRepository<Prize>().Entities
                .Where(p => p.Registration.HorseId == horseId && !p.Registration.Horse.IsDeleted);

            int rewardCount = await rewardQuery.CountAsync();
            decimal totalRewardAmount = await rewardQuery.SumAsync(p => p.Amount ?? 0);

            List<HorseRewardItemResponse> items = await rewardQuery
                .OrderByDescending(p => p.DistributedAt ?? DateTimeOffset.MinValue)
                .ThenByDescending(p => p.PrizeId)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(p => new HorseRewardItemResponse
                {
                    PrizeId = p.PrizeId,
                    RegistrationId = p.RegistrationId,
                    RaceId = p.Registration.RaceId,
                    RaceNumber = p.Registration.Race.RaceNumber,
                    TournamentId = p.Registration.Race.TournamentId,
                    TournamentName = p.Registration.Race.Tournament.TournamentName,
                    PrizeType = p.PrizeType,
                    Amount = p.Amount,
                    DistributedAt = p.DistributedAt
                })
                .ToListAsync();

            return new HorseRewardsResponse
            {
                HorseId = horse.Id,
                HorseName = horse.HorseName,
                TotalRewardAmount = totalRewardAmount,
                RewardCount = rewardCount,
                Rewards = new PagedResponse<HorseRewardItemResponse>
                {
                    Items = items,
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalCount = rewardCount
                }
            };
        }

        private IQueryable<Horse> BuildHorseScope(Guid accountId, bool isAdmin)
        {
            IQueryable<Horse> query = _uow.GetRepository<Horse>().Entities
                .Where(h => !h.IsDeleted);

            if (!isAdmin)
                query = query.Where(h => h.OwnerId == accountId);

            return query;
        }

        private async Task<Horse> GetHorseEntityAsync(Guid horseId, Guid accountId, bool isAdmin)
        {
            Horse? horse = await _uow.GetRepository<Horse>().Entities
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

        private IQueryable<Registration> BuildScheduleQuery()
        {
            return _uow.GetRepository<Registration>().Entities
                .Where(r => !r.Horse.IsDeleted && !r.Race.IsDeleted)
                .Include(r => r.Horse)
                .Include(r => r.Race)
                    .ThenInclude(r => r.Racecourse)
                .Include(r => r.Race)
                    .ThenInclude(r => r.Tournament);
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

        private static void NormalizePaging(HorseRewardsQueryRequest query)
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
            Status = horse.Status,
            RecordWins = horse.RecordWins,
            Color = horse.Color,
            CreateAt = horse.CreateAt,
            UpdatedAt = horse.UpdatedAt
        };

        private static HorseScheduleResponse MapToScheduleResponse(Registration registration) => new HorseScheduleResponse
        {
            HorseId = registration.HorseId,
            HorseName = registration.Horse.HorseName,
            RegistrationId = registration.RegistrationId,
            JockeyId = registration.JockeyId,
            GateNumber = registration.GateNumber,
            OwnerConfirmation = registration.OwnerConfirmation,
            JockeyConfirmation = registration.JockeyConfirmation,
            RegistrationStatus = registration.Status,
            RaceId = registration.RaceId,
            RaceNumber = registration.Race.RaceNumber,
            StartTime = registration.Race.StartTime,
            TrackLength = registration.Race.TrackLength,
            MaxParticipants = registration.Race.MaxParticipants,
            RaceStatus = registration.Race.Status,
            RacecourseName = registration.Race.Racecourse.RacecourseName,
            Location = registration.Race.Racecourse.Location,
            TournamentId = registration.Race.TournamentId,
            TournamentName = registration.Race.Tournament.TournamentName
        };

    }
}
