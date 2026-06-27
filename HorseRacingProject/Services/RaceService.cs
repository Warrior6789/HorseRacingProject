using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Helpers;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Middlewares;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class RaceService : IRaceService
    {
        private readonly IUnitofWork _uow;
        private readonly RaceEngineService _engine;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IHubContext<RaceHub> _hubContext;

        public RaceService(IUnitofWork uow, RaceEngineService engine, ICloudinaryService cloudinaryService, IHubContext<RaceHub> hubContext)
        {
            _uow = uow;
            _engine = engine;
            _cloudinaryService = cloudinaryService;
            _hubContext = hubContext;
        }

        public async Task<PagedResponse<RaceResponse>> GetRacesAsync(int page, int pageSize, Guid? racecourseId, string? status, string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            RaceStatus? parsedStatus = status != null && Enum.TryParse<RaceStatus>(status, ignoreCase: true, out var s) ? s : (RaceStatus?)null;
            string? searchTrim = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();

            IGenericRepository<Race> repo = _uow.GetRepository<Race>();

            int totalCount = await repo.Entities
                .CountAsync(r => !r.IsDeleted
                    && (racecourseId == null || r.RacecourseId == racecourseId)
                    && (parsedStatus == null || r.Status == parsedStatus)
                    && (searchTrim == null || r.RaceName!.ToLower().Contains(searchTrim)));

            IEnumerable<RaceResponse> items = await repo.FindAsync<RaceResponse>(
                predicate: r => !r.IsDeleted
                    && (racecourseId == null || r.RacecourseId == racecourseId)
                    && (parsedStatus == null || r.Status == parsedStatus)
                    && (searchTrim == null || r.RaceName!.ToLower().Contains(searchTrim)),
                orderBy: q => q.OrderBy(r => r.StartTime),
                selector: r => new RaceResponse
                {
                    RaceId = r.RaceId,
                    RaceNumber = r.RaceNumber,
                    RaceName = r.RaceName,
                    StartTime = r.StartTime,
                    TrackLength = r.TrackLength,
                    MaxParticipants = r.MaxParticipants,
                    Status = r.Status.ToString(),
                    RegistrationFee = r.RegistrationFee,
                    PrizePool = r.PrizePool,
                    RacecourseName = r.Racecourse.RacecourseName,
                    Location = r.Racecourse.Location,
                    ImageUrl = r.ImageUrl,
                    RegistrationCount = r.Registrations.Count(reg => reg.Status == RegistrationStatus.Confirmed)
                },
                pageIndex: page - 1,
                pageSize: pageSize
            );

            return new PagedResponse<RaceResponse>
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<RaceResponse> GetRaceByIdAsync(Guid raceId)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .Include(r => r.Racecourse)
                .Include(r => r.Registrations)
                .Include(r => r.RefereeReports)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            RaceResponse response = MapToResponse(race);
            response.HasUnresolvedReports = race.RefereeReports
                .Any(r => r.Status == RefereeReportStatus.Pending);
            return response;
        }

        public async Task<RaceResponse> CreateRaceAsync(CreateRaceRequest request)
        {
            Racecourse? racecourse = await _uow.GetRepository<Racecourse>().Entities
                .FirstOrDefaultAsync(rc => rc.Id == request.RacecourseId && !rc.IsDeleted);
            if (racecourse == null)
                throw new KeyNotFoundException($"Racecourse with id {request.RacecourseId} not found.");

            if (request.StartTime <= DateTimeOffset.UtcNow.AddMinutes(90))
                throw new InvalidOperationException("Start time must be at least 90 minutes from now.");

            bool duplicate = await _uow.GetRepository<Race>().Entities
                .AnyAsync(r => r.RacecourseId == request.RacecourseId
                    && r.RaceNumber == request.RaceNumber
                    && !r.IsDeleted);
            if (duplicate)
                throw new InvalidOperationException($"Race number {request.RaceNumber} already exists at this racecourse.");

            DateTimeOffset newRaceBlockEnd = request.StartTime.AddMinutes(10);

            Race? conflictingRace = await _uow.GetRepository<Race>().Entities
                .Where(r => r.RacecourseId == request.RacecourseId
                    && r.Status != RaceStatus.Cancelled
                    && !r.IsDeleted
                    && r.StartTime.HasValue
                    && r.StartTime < newRaceBlockEnd
                    && (r.EndTime ?? r.StartTime!.Value.AddMinutes(5)).AddMinutes(5) > request.StartTime)
                .FirstOrDefaultAsync();

            if (conflictingRace != null)
            {
                DateTimeOffset conflictEnd = (conflictingRace.EndTime ?? conflictingRace.StartTime!.Value.AddMinutes(5)).AddMinutes(5);
                throw new InvalidOperationException($"Time slot conflicts with an existing race at this racecourse. Next available slot after {conflictEnd:yyyy-MM-dd HH:mm} UTC.");
            }

            string? imageUrl = null;
            if (request.Image != null)
                imageUrl = await _cloudinaryService.UploadImageAsync(request.Image, "races");

            RegistrationFeeConfig? feeConfig = await _uow.GetRepository<RegistrationFeeConfig>().Entities
                .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active);

            Race race = new Race
            {
                RaceId = Guid.NewGuid(),
                RacecourseId = request.RacecourseId,
                RaceNumber = request.RaceNumber,
                RaceName = request.RaceName,
                StartTime = request.StartTime,
                TrackLength = request.TrackLength,
                MaxParticipants = request.MaxParticipants,
                RegistrationFeeConfigId = feeConfig?.RegistrationFeeConfigId,
                RegistrationFee = feeConfig?.FeeAmount ?? 0,
                PrizePool = 0,
                Status = RaceStatus.Scheduled,
                CreateAt = DateTimeOffset.UtcNow,
                IsDeleted = false,
                ImageUrl = imageUrl
            };

            await _uow.GetRepository<Race>().AddAsync(race);
            await _uow.SaveAsync();
            await _hubContext.Clients.All.SendAsync("RacesUpdated");

            race.Racecourse = racecourse;

            return MapToResponse(race);
        }

        public async Task<RaceResponse> UpdateRaceAsync(Guid raceId, UpdateRaceRequest request)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .Include(r => r.Racecourse)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            if (race.Status != RaceStatus.Scheduled)
                throw new InvalidOperationException("Race can only be updated when it is in Scheduled status.");

            if (request.RacecourseId.HasValue)
            {
                Racecourse? racecourse = await _uow.GetRepository<Racecourse>().Entities
                    .FirstOrDefaultAsync(rc => rc.Id == request.RacecourseId && !rc.IsDeleted);
                if (racecourse == null)
                    throw new KeyNotFoundException($"Racecourse with id {request.RacecourseId} not found.");
                race.RacecourseId = request.RacecourseId.Value;
                race.Racecourse = racecourse;
            }

            if (request.RaceNumber.HasValue)
            {
                bool duplicate = await _uow.GetRepository<Race>().Entities
                    .AnyAsync(r => r.RacecourseId == race.RacecourseId
                        && r.RaceNumber == request.RaceNumber
                        && r.RaceId != raceId
                        && !r.IsDeleted);
                if (duplicate)
                    throw new InvalidOperationException($"Race number {request.RaceNumber} already exists at this racecourse.");
                race.RaceNumber = request.RaceNumber;
            }

            if (request.RaceName != null)
                race.RaceName = request.RaceName;

            if (request.StartTime.HasValue)
            {
                if (request.StartTime.Value <= DateTimeOffset.UtcNow.AddMinutes(90))
                    throw new InvalidOperationException("Start time must be at least 90 minutes from now.");

                DateTimeOffset updatedRaceBlockEnd = request.StartTime.Value.AddMinutes(10);

                Race? conflictingRace = await _uow.GetRepository<Race>().Entities
                    .Where(r => r.RacecourseId == race.RacecourseId
                        && r.RaceId != raceId
                        && r.Status != RaceStatus.Cancelled
                        && !r.IsDeleted
                        && r.StartTime.HasValue
                        && r.StartTime < updatedRaceBlockEnd
                        && (r.EndTime ?? r.StartTime!.Value.AddMinutes(5)).AddMinutes(5) > request.StartTime.Value)
                    .FirstOrDefaultAsync();

                if (conflictingRace != null)
                {
                    DateTimeOffset conflictEnd = (conflictingRace.EndTime ?? conflictingRace.StartTime!.Value.AddMinutes(5)).AddMinutes(5);
                    throw new InvalidOperationException($"Time slot conflicts with an existing race at this racecourse. Next available slot after {conflictEnd:yyyy-MM-dd HH:mm} UTC.");
                }

                race.StartTime = request.StartTime;
            }

            if (request.TrackLength.HasValue)
                race.TrackLength = request.TrackLength;

            if (request.MaxParticipants.HasValue)
                race.MaxParticipants = request.MaxParticipants;

            await _uow.GetRepository<Race>().UpdateAsync(race);
            await _uow.SaveAsync();
            await _hubContext.Clients.All.SendAsync("RacesUpdated");

            return MapToResponse(race);
        }

        public async Task DeleteRaceAsync(Guid raceId)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            if (race.Status == RaceStatus.Live)
                throw new InvalidOperationException("Cannot delete a race that is currently Live.");

            race.IsDeleted = true;
            race.DeletedAt = DateTimeOffset.UtcNow;
            await _uow.GetRepository<Race>().UpdateAsync(race);
            await _uow.SaveAsync();
        }

        public async Task<RegistrationResponse> RegisterHorseAsync(Guid raceId, Guid ownerId, RegisterHorseToRaceRequest request)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .Include(r => r.Racecourse)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            if (race.Status != RaceStatus.Scheduled)
                throw new InvalidOperationException("Horses can only be registered when race is in Scheduled status.");

            Horse? horse = await _uow.GetRepository<Horse>().Entities
                .FirstOrDefaultAsync(h => h.Id == request.HorseId && !h.IsDeleted);
            if (horse == null)
                throw new KeyNotFoundException($"Horse with id {request.HorseId} not found.");

            if (horse.OwnerId != ownerId)
                throw new ForbiddenAccessException("You do not own this horse.");

            if (!HorseStatusPolicy.CanRegisterForRace(horse.Status))
                throw new InvalidOperationException("Only Healthy or Resting horses can be registered for a race.");

            Account? jockey = await _uow.GetRepository<Account>().Entities
                .FirstOrDefaultAsync(a => a.Id == request.JockeyId && !a.IsDeleted);
            if (jockey == null)
                throw new KeyNotFoundException($"Jockey with id {request.JockeyId} not found.");

            if (jockey.Role != AccountRole.Jockey)
                throw new InvalidOperationException("The specified account is not a Jockey.");

            bool hasProfile = await _uow.GetRepository<JockeyProfile>().Entities
                .AnyAsync(p => p.AccountId == request.JockeyId && !p.IsDeleted);
            if (!hasProfile)
                throw new InvalidOperationException("Jockey does not have a profile yet.");

            if (race.MaxParticipants.HasValue)
            {
                int activeCount = await _uow.GetRepository<Registration>().Entities
                    .CountAsync(r => r.RaceId == raceId
                        && (r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending));
                if (activeCount >= race.MaxParticipants.Value)
                    throw new InvalidOperationException($"Race has reached the maximum number of participants ({race.MaxParticipants}).");
            }

            if (request.GateNumber.HasValue)
            {
                bool gateTaken = await _uow.GetRepository<Registration>().Entities
                    .AnyAsync(r => r.RaceId == raceId
                        && r.GateNumber == request.GateNumber
                        && r.Status != RegistrationStatus.Rejected
                        && r.Status != RegistrationStatus.Scratched);
                if (gateTaken)
                    throw new InvalidOperationException($"Gate number {request.GateNumber} is already taken.");
            }

            bool horseConfirmed = await _uow.GetRepository<Registration>().Entities
                .AnyAsync(r => r.HorseId == request.HorseId
                    && r.Status == RegistrationStatus.Confirmed);
            if (horseConfirmed)
                throw new InvalidOperationException("This horse is already confirmed in a race.");

            bool horsePendingElsewhere = await _uow.GetRepository<Registration>().Entities
                .AnyAsync(r => r.HorseId == request.HorseId
                    && r.Status == RegistrationStatus.Pending
                    && r.Race.RacecourseId != race.RacecourseId);
            if (horsePendingElsewhere)
                throw new InvalidOperationException("This horse already has a pending registration at another racecourse.");

            DateTimeOffset? lastRaceEnd = await _uow.GetRepository<Registration>().Entities
                .Where(r => r.HorseId == request.HorseId
                    && r.Status == RegistrationStatus.Confirmed
                    && r.Race.Status == RaceStatus.Finished)
                .MaxAsync(r => (DateTimeOffset?)r.Race.EndTime);

            if (lastRaceEnd != null && race.StartTime < lastRaceEnd.Value.AddDays(7))
                throw new InvalidOperationException($"Horse needs 7 days rest after last race. Earliest registration date: {lastRaceEnd.Value.AddDays(7):yyyy-MM-dd}.");

            bool ownerAlreadyRegistered = await _uow.GetRepository<Registration>().Entities
                .AnyAsync(r => r.RaceId == raceId
                    && r.Horse.OwnerId == ownerId
                    && (r.Status == RegistrationStatus.Pending || r.Status == RegistrationStatus.Confirmed));
            if (ownerAlreadyRegistered)
                throw new InvalidOperationException("You have already registered a horse in this race.");

            bool jockeyConfirmedInSameRace = await _uow.GetRepository<Registration>().Entities
                .AnyAsync(r => r.JockeyId == request.JockeyId
                    && r.RaceId == raceId
                    && r.Status == RegistrationStatus.Confirmed);
            if (jockeyConfirmedInSameRace)
                throw new InvalidOperationException("This jockey is already confirmed in this race.");

            var confirmedElsewhere = await _uow.GetRepository<Registration>().Entities
                .Where(r => r.JockeyId == request.JockeyId
                    && r.RaceId != raceId
                    && r.Status == RegistrationStatus.Confirmed)
                .Select(r => new { r.Race.StartTime, r.Race.EndTime, r.Race.RacecourseId })
                .ToListAsync();

            foreach (var other in confirmedElsewhere)
            {
                bool sameVenue = other.RacecourseId == race.RacecourseId;
                DateTimeOffset effectiveStart = sameVenue ? race.StartTime!.Value : race.StartTime!.Value.AddHours(-2);
                DateTimeOffset effectiveEnd   = sameVenue ? race.EndTime!.Value   : race.EndTime!.Value.AddHours(2);

                if (other.StartTime < effectiveEnd && other.EndTime > effectiveStart)
                    throw new InvalidOperationException("This jockey is already confirmed in another race that conflicts in time.");
            }

            if (race.RegistrationFee > 0)
            {
                UserProfile? ownerProfile = await _uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == ownerId && !p.IsDeleted);
                if (ownerProfile == null || (ownerProfile.Balance ?? 0) < (long)race.RegistrationFee)
                    throw new InvalidOperationException($"Insufficient balance. Registration fee is {race.RegistrationFee} coins.");
                ownerProfile.Balance = (ownerProfile.Balance ?? 0) - (long)race.RegistrationFee;
                ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                await _uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);
                race.PrizePool += race.RegistrationFee;
                await _uow.GetRepository<Race>().UpdateAsync(race);
            }

            Registration registration = new Registration
            {
                RegistrationId = Guid.NewGuid(),
                RaceId = raceId,
                HorseId = request.HorseId,
                JockeyId = request.JockeyId,
                GateNumber = request.GateNumber,
                OwnerConfirmation = true,
                JockeyConfirmation = null,
                Status = RegistrationStatus.Pending,
                CreateAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _uow.GetRepository<Registration>().AddAsync(registration);
            await _uow.SaveAsync();

            int pendingCount = await _uow.GetRepository<Registration>().Entities
                .CountAsync(r => r.Status == RegistrationStatus.Pending);
            await _hubContext.Clients.All.SendAsync("RegistrationsUpdated", new
            {
                pendingCount,
                approvedCount = await _uow.GetRepository<Registration>().Entities.CountAsync(r => r.Status == RegistrationStatus.Confirmed),
                rejectedCount = await _uow.GetRepository<Registration>().Entities.CountAsync(r => r.Status == RegistrationStatus.Rejected),
                jockeyId = request.JockeyId
            });

            return new RegistrationResponse
            {
                RegistrationId = registration.RegistrationId,
                JockeyId = registration.JockeyId,
                GateNumber = registration.GateNumber,
                OwnerConfirmation = registration.OwnerConfirmation,
                JockeyConfirmation = registration.JockeyConfirmation,
                Status = registration.Status.ToString(),
                CreateAt = registration.CreateAt,
                UpdatedAt = registration.UpdatedAt,
                Horse = new HorseResponse
                {
                    Id = horse.Id,
                    HorseName = horse.HorseName,
                    Breed = horse.Breed,
                    Color = horse.Color,
                    Age = horse.Age,
                    Weight = horse.Weight,
                    RecordWins = horse.RecordWins,
                    Status = horse.Status.ToString(),
                    DerivedStatus = horse.Status.ToString()
                },
                Race = MapToResponse(race)
            };
        }

        public async Task<PagedResponse<UpcomingRaceResponse>> GetUpcomingRacesAsync(int page, int pageSize, List<string>? statuses)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            List<RaceStatus> allowedStatuses = (statuses == null || statuses.Count == 0)
                ? [RaceStatus.Scheduled, RaceStatus.BettingOpen, RaceStatus.BettingClosed]
                : statuses
                    .Where(s => Enum.TryParse<RaceStatus>(s, ignoreCase: true, out _))
                    .Select(s => Enum.Parse<RaceStatus>(s, ignoreCase: true))
                    .ToList();

            IGenericRepository<Race> repo = _uow.GetRepository<Race>();

            int totalCount = await repo.Entities
                .CountAsync(r => !r.IsDeleted && allowedStatuses.Contains(r.Status));

            IEnumerable<UpcomingRaceResponse> items = await repo.FindAsync<UpcomingRaceResponse>(
                predicate: r => !r.IsDeleted && allowedStatuses.Contains(r.Status),
                orderBy: q => q.OrderBy(r => r.StartTime),
                selector: r => new UpcomingRaceResponse
                {
                    RaceId = r.RaceId,
                    RaceName = r.RaceName,
                    StartTime = r.StartTime,
                    TrackLength = r.TrackLength,
                    MaxParticipants = r.MaxParticipants,
                    Status = r.Status.ToString(),
                    RacecourseName = r.Racecourse.RacecourseName,
                    TrackType = r.Racecourse.TrackType,
                    Location = r.Racecourse.Location
                },
                pageIndex: page - 1,
                pageSize: pageSize
            );

            return new PagedResponse<UpcomingRaceResponse>
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<List<RaceResultResponse>> GetRaceResultsAsync(Guid raceId)
        {
            bool exists = await _uow.GetRepository<Race>().Entities
                .AnyAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (!exists)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            return await _uow.GetRepository<RaceResult>().Entities
                .Include(r => r.Registration)
                    .ThenInclude(reg => reg.Horse)
                .Where(r => r.Registration.RaceId == raceId)
                .OrderBy(r => r.FinishPosition)
                .Select(r => new RaceResultResponse
                {
                    Position = r.FinishPosition,
                    Horse = new RaceResultHorseDto
                    {
                        Id = r.Registration.Horse.Id,
                        RegistrationId = r.RegistrationId,
                        HorseName = r.Registration.Horse.HorseName,
                        Breed = r.Registration.Horse.Breed,
                        Color = r.Registration.Horse.Color,
                        Age = r.Registration.Horse.Age,
                        Status = r.Registration.Horse.Status.ToString()
                    },
                    FinishedAt = r.CreateAt.HasValue
                        ? r.CreateAt.Value.ToString("o")
                        : null
                })
                .ToListAsync();
        }

        public async Task<List<RaceResultHorseDto>> GetRaceHorsesAsync(Guid raceId)
        {
            bool exists = await _uow.GetRepository<Race>().Entities
                .AnyAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (!exists)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            return await _uow.GetRepository<Registration>().Entities
                .Include(r => r.Horse)
                .Where(r => r.RaceId == raceId && r.Status == RegistrationStatus.Confirmed)
                .Select(r => new RaceResultHorseDto
                {
                    Id = r.Horse.Id,
                    RegistrationId = r.RegistrationId,
                    HorseName = r.Horse.HorseName,
                    Breed = r.Horse.Breed,
                    Color = r.Horse.Color,
                    Age = r.Horse.Age,
                    Status = r.Horse.Status.ToString()
                })
                .ToListAsync();
        }

        public async Task<List<RegistrationResponse>> GetRaceRegistrationsAsync(Guid raceId)
        {
            bool exists = await _uow.GetRepository<Race>().Entities
                .AnyAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (!exists)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            return await _uow.GetRepository<Registration>().Entities
                .Include(r => r.Horse)
                .Include(r => r.Race)
                    .ThenInclude(r => r.Racecourse)
                .Include(r => r.Jockey)
                    .ThenInclude(a => a.JockeyProfiles)
                .Where(r => r.RaceId == raceId && r.Status == RegistrationStatus.Confirmed)
                .OrderBy(r => r.GateNumber)
                .Select(r => new RegistrationResponse
                {
                    RegistrationId = r.RegistrationId,
                    JockeyId = r.JockeyId,
                    JockeyName = r.Jockey.JockeyProfiles.Where(p => !p.IsDeleted).Select(p => p.FullName).FirstOrDefault(),
                    GateNumber = r.GateNumber,
                    Horse = new HorseResponse
                    {
                        Id = r.HorseId,
                        HorseName = r.Horse.HorseName,
                        Breed = r.Horse.Breed,
                        Color = r.Horse.Color,
                        Age = r.Horse.Age,
                        Weight = r.Horse.Weight,
                        RecordWins = r.Horse.RecordWins,
                        Status = r.Horse.Status.ToString(),
                        DerivedStatus = r.Horse.Status.ToString(),
                        ImageUrl = r.Horse.ImageUrl
                    },
                    Race = new RaceResponse
                    {
                        RaceId = r.RaceId,
                        RaceNumber = r.Race.RaceNumber,
                        RaceName = r.Race.RaceName,
                        StartTime = r.Race.StartTime,
                        TrackLength = r.Race.TrackLength,
                        MaxParticipants = r.Race.MaxParticipants,
                        Status = r.Race.Status.ToString(),
                        RacecourseName = r.Race.Racecourse.RacecourseName,
                        Location = r.Race.Racecourse.Location
                    },
                    OwnerConfirmation = r.OwnerConfirmation,
                    JockeyConfirmation = r.JockeyConfirmation,
                    Status = r.Status.ToString(),
                    CreateAt = r.CreateAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task ResetRaceAsync(Guid raceId)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .Include(r => r.Racecourse)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            if (race.Status == RaceStatus.Scheduled)
                throw new InvalidOperationException("Race is already in Scheduled status.");

            List<Registration> registrations = await _uow.GetRepository<Registration>().Entities
                .Where(r => r.RaceId == raceId)
                .ToListAsync();

            List<Guid> horseIds = registrations.Select(r => r.HorseId).ToList();

            List<Bet> bets = await _uow.GetRepository<Bet>().Entities
                .Include(b => b.Registration)
                .Where(b => b.Registration.RaceId == raceId)
                .ToListAsync();

            foreach (Bet bet in bets)
            {
                UserProfile? profile = await _uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == bet.SpectatorId && !p.IsDeleted);

                if (profile != null)
                {
                    if (bet.Status == BetStatus.Won)
                    {
                        long payout = (long)(bet.BetAmount * (decimal)(bet.PayoutRatio ?? 1));
                        profile.Balance = Math.Max(0, (profile.Balance ?? 0) - payout + (long)bet.BetAmount);
                    }
                    else if (bet.Status == BetStatus.Lost || bet.Status == BetStatus.Active)
                    {
                        profile.Balance = (profile.Balance ?? 0) + (long)bet.BetAmount;
                    }
                    profile.UpdatedAt = DateTimeOffset.UtcNow;
                    await _uow.GetRepository<UserProfile>().UpdateAsync(profile);
                }
            }

            await _uow.GetRepository<Bet>().DeleteRangeAsync(bets);

            List<RaceResult> results = await _uow.GetRepository<RaceResult>().Entities
                .Include(r => r.Registration)
                .Where(r => r.Registration.RaceId == raceId)
                .ToListAsync();

            await _uow.GetRepository<RaceResult>().DeleteRangeAsync(results);

            List<RefereeReport> reports = await _uow.GetRepository<RefereeReport>().Entities
                .Where(r => r.RaceId == raceId)
                .ToListAsync();

            await _uow.GetRepository<RefereeReport>().DeleteRangeAsync(reports);

            List<Prize> prizes = await _uow.GetRepository<Prize>().Entities
                .Include(p => p.Registration).ThenInclude(r => r.Horse)
                .Where(p => p.Registration.RaceId == raceId)
                .ToListAsync();

            foreach (Prize prize in prizes)
            {
                if (prize.Amount == null || prize.Amount == 0) continue;

                Guid recipientId = prize.PrizeType == PrizeType.Jockey
                    ? prize.Registration.JockeyId
                    : prize.Registration.Horse.OwnerId;

                UserProfile? profile = await _uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == recipientId && !p.IsDeleted);

                if (profile != null)
                {
                    long delta = (long)Math.Round(Math.Abs(prize.Amount.Value));
                    profile.Balance = prize.Amount > 0
                        ? Math.Max(0, (profile.Balance ?? 0) - delta)
                        : (profile.Balance ?? 0) + delta;
                    profile.UpdatedAt = DateTimeOffset.UtcNow;
                    await _uow.GetRepository<UserProfile>().UpdateAsync(profile);
                }
            }

            await _uow.GetRepository<Prize>().DeleteRangeAsync(prizes);

            List<RacePool> racePools = await _uow.GetRepository<RacePool>().Entities
                .Where(p => p.RaceId == raceId)
                .ToListAsync();

            await _uow.GetRepository<RacePool>().DeleteRangeAsync(racePools);

            race.Status = RaceStatus.Scheduled;
            race.EndTime = null;
            race.StartTime = DateTimeOffset.UtcNow.AddHours(24);
            await _uow.GetRepository<Race>().UpdateAsync(race);

            await _uow.SaveAsync();

            _engine.ClearRaceState(raceId, horseIds);
        }

        public async Task<RaceResponse> AdvanceRaceStatusAsync(Guid raceId)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .Include(r => r.Racecourse)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            RaceStatus? nextStatus = race.Status switch
            {
                RaceStatus.Scheduled     => RaceStatus.BettingOpen,
                RaceStatus.BettingOpen   => RaceStatus.BettingClosed,
                RaceStatus.BettingClosed => RaceStatus.Live,
                _                        => (RaceStatus?)null
            };

            if (nextStatus == null)
                throw new InvalidOperationException($"Race is in '{race.Status}' status and cannot be advanced.");

            if (nextStatus == RaceStatus.BettingOpen)
            {
                PositionPrizeConfig? posConfig = await _uow.GetRepository<PositionPrizeConfig>().Entities
                    .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active);
                JockeyRewardConfig? jockeyConfig = await _uow.GetRepository<JockeyRewardConfig>().Entities
                    .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active);

                if (posConfig == null || jockeyConfig == null)
                    throw new InvalidOperationException("Position prize config and jockey reward config must be active before opening betting.");

                race.PositionPrizeConfigId = posConfig.PositionPrizeConfigId;
                race.JockeyRewardConfigId = jockeyConfig.JockeyRewardConfigId;
            }

            if (nextStatus == RaceStatus.Live)
            {
                int confirmedCount = await _uow.GetRepository<Registration>().Entities
                    .CountAsync(r => r.RaceId == raceId && r.Status == RegistrationStatus.Confirmed);

                if (confirmedCount < 3)
                    throw new InvalidOperationException("Race must have at least 3 confirmed registrations before going Live.");

                bool otherRaceLive = await _uow.GetRepository<Race>().Entities
                    .AnyAsync(r => r.RacecourseId == race.RacecourseId
                                && r.RaceId != raceId
                                && r.Status == RaceStatus.Live
                                && !r.IsDeleted);

                if (otherRaceLive)
                    throw new InvalidOperationException("Another race at the same racecourse is currently Live.");

                if (!race.RefereeId.HasValue)
                    throw new InvalidOperationException("Race must have a referee assigned before going Live.");

                if (race.RefereeId.HasValue)
                {
                    var conflictingRace = await _uow.GetRepository<Race>().Entities
                        .Where(r => r.RefereeId == race.RefereeId
                                 && r.RaceId != raceId
                                 && r.Status == RaceStatus.Live
                                 && !r.IsDeleted)
                        .Select(r => new { r.RaceNumber })
                        .FirstOrDefaultAsync();

                    if (conflictingRace != null)
                        throw new InvalidOperationException(
                            $"Cannot go Live: assigned referee is still active in Race #{conflictingRace.RaceNumber}.");
                }
            }

            race.Status = nextStatus.Value;
            await _uow.GetRepository<Race>().UpdateAsync(race);
            await _uow.SaveAsync();
            await _hubContext.Clients.All.SendAsync("RacesUpdated");

            return MapToResponse(race);
        }


        public async Task<string> UploadImageAsync(Guid raceId, IFormFile file)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);
            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            string? oldImageUrl = race.ImageUrl;
            string newImageUrl = await _cloudinaryService.UploadImageAsync(file, "races");

            try
            {
                race.ImageUrl = newImageUrl;
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

        private static RaceResponse MapToResponse(Race race) => new RaceResponse
        {
            RaceId = race.RaceId,
            RaceNumber = race.RaceNumber,
            RaceName = race.RaceName,
            StartTime = race.StartTime,
            TrackLength = race.TrackLength,
            MaxParticipants = race.MaxParticipants,
            Status = race.Status.ToString(),
            RegistrationFee = race.RegistrationFee,
            PrizePool = race.PrizePool,
            RacecourseName = race.Racecourse?.RacecourseName,
            Location = race.Racecourse?.Location,
            ImageUrl = race.ImageUrl,
            RegistrationCount = race.Registrations?.Count(reg => reg.Status == RegistrationStatus.Confirmed) ?? 0
        };
    }
}
