using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Middlewares;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class RaceService : IRaceService
    {
        private readonly IUnitofWork _uow;
        private readonly RaceEngineService _engine;

        private static readonly Dictionary<string, double> _gradePurseRatio = new()
        {
            ["G1"]     = 0.50,
            ["G2"]     = 0.30,
            ["G3"]     = 0.15,
            ["Listed"] = 0.05,
            ["Open"]   = 0.03
        };

        public RaceService(IUnitofWork uow, RaceEngineService engine)
        {
            _uow = uow;
            _engine = engine;
        }

        public async Task<PagedResponse<RaceResponse>> GetRacesAsync(int page, int pageSize, Guid? tournamentId, Guid? racecourseId, string? status)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<Race> repo = _uow.GetRepository<Race>();

            int totalCount = await repo.Entities
                .CountAsync(r => !r.IsDeleted
                    && (tournamentId == null || r.TournamentId == tournamentId)
                    && (racecourseId == null || r.RacecourseId == racecourseId)
                    && (status == null || r.Status == status));

            IEnumerable<RaceResponse> items = await repo.FindAsync<RaceResponse>(
                predicate: r => !r.IsDeleted
                    && (tournamentId == null || r.TournamentId == tournamentId)
                    && (racecourseId == null || r.RacecourseId == racecourseId)
                    && (status == null || r.Status == status),
                orderBy: q => q.OrderBy(r => r.StartTime),
                selector: r => new RaceResponse
                {
                    RaceId = r.RaceId,
                    RaceNumber = r.RaceNumber,
                    StartTime = r.StartTime,
                    TrackLength = r.TrackLength,
                    MaxParticipants = r.MaxParticipants,
                    Status = r.Status,
                    Grade = r.Grade,
                    RacecourseName = r.Racecourse.RacecourseName,
                    Location = r.Racecourse.Location,
                    Tournament = new TournamentResponse
                    {
                        TournamentId = r.Tournament.Id,
                        TournamentName = r.Tournament.TournamentName,
                        Description = r.Tournament.Description,
                        StartDate = r.Tournament.StartDate,
                        EndDate = r.Tournament.EndDate,
                        Status = r.Tournament.Status,
                        FundsPrize = r.Tournament.FundsPrize
                    }
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
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            return MapToResponse(race);
        }

        public async Task<List<RaceResponse>> GetRacesByTournamentAsync(Guid tournamentId)
        {
            bool exists = await _uow.GetRepository<Tournament>().Entities
                .AnyAsync(t => t.Id == tournamentId && !t.IsDeleted);
            if (!exists)
                throw new KeyNotFoundException($"Tournament with id {tournamentId} not found.");

            return await _uow.GetRepository<Race>().Entities
                .Where(r => r.TournamentId == tournamentId && !r.IsDeleted)
                .OrderBy(r => r.RaceNumber)
                .Select(r => new RaceResponse
                {
                    RaceId = r.RaceId,
                    RaceNumber = r.RaceNumber,
                    StartTime = r.StartTime,
                    TrackLength = r.TrackLength,
                    MaxParticipants = r.MaxParticipants,
                    Status = r.Status,
                    Grade = r.Grade,
                    RacecourseName = r.Racecourse.RacecourseName,
                    Location = r.Racecourse.Location,
                    Tournament = new TournamentResponse
                    {
                        TournamentId = r.Tournament.Id,
                        TournamentName = r.Tournament.TournamentName,
                        Description = r.Tournament.Description,
                        StartDate = r.Tournament.StartDate,
                        EndDate = r.Tournament.EndDate,
                        Status = r.Tournament.Status,
                        FundsPrize = r.Tournament.FundsPrize
                    }
                })
                .ToListAsync();
        }

        public async Task<RaceResponse> CreateRaceAsync(CreateRaceRequest request)
        {
            Tournament? tournament = await _uow.GetRepository<Tournament>().Entities
                .FirstOrDefaultAsync(t => t.Id == request.TournamentId && !t.IsDeleted);
            if (tournament == null)
                throw new KeyNotFoundException($"Tournament with id {request.TournamentId} not found.");

            Racecourse? racecourse = await _uow.GetRepository<Racecourse>().Entities
                .FirstOrDefaultAsync(rc => rc.Id == request.RacecourseId && !rc.IsDeleted);
            if (racecourse == null)
                throw new KeyNotFoundException($"Racecourse with id {request.RacecourseId} not found.");

            if (request.StartTime <= DateTimeOffset.UtcNow.AddMinutes(30))
                throw new InvalidOperationException("Start time must be at least 30 minutes from now.");

            bool duplicate = await _uow.GetRepository<Race>().Entities
                .AnyAsync(r => r.TournamentId == request.TournamentId
                    && r.RaceNumber == request.RaceNumber
                    && !r.IsDeleted);
            if (duplicate)
                throw new InvalidOperationException($"Race number {request.RaceNumber} already exists in this tournament.");

            List<string?> existingGrades = await _uow.GetRepository<Race>().Entities
                .Where(r => r.TournamentId == request.TournamentId && !r.IsDeleted && r.Status != "Cancelled")
                .Select(r => r.Grade)
                .ToListAsync();

            decimal totalExpected = existingGrades
                .Sum(g => tournament.FundsPrize * (decimal)_gradePurseRatio.GetValueOrDefault(g ?? "Open", 0.03));

            decimal newRacePurse = tournament.FundsPrize * (decimal)_gradePurseRatio.GetValueOrDefault(request.Grade.ToString(), 0.03);

            if (totalExpected + newRacePurse > tournament.FundsPrize)
                throw new InvalidOperationException(
                    $"Cannot create race. Total expected prize ({totalExpected + newRacePurse:N0}) would exceed tournament funds ({tournament.FundsPrize:N0}).");

            Race? latestRace = await _uow.GetRepository<Race>().Entities
                .Where(r => r.RacecourseId == request.RacecourseId
                    && r.Status != "Cancelled"
                    && !r.IsDeleted)
                .OrderByDescending(r => r.StartTime)
                .FirstOrDefaultAsync();

            if (latestRace != null)
            {
                DateTimeOffset latestEnd = (latestRace.EndTime ?? latestRace.StartTime!.Value.AddMinutes(5)).AddMinutes(5);
                if (request.StartTime < latestEnd)
                    throw new InvalidOperationException($"This racecourse already has a race scheduled. Next race can start after {latestEnd:HH:mm} UTC.");
            }

            Race race = new Race
            {
                TournamentId = request.TournamentId,
                RacecourseId = request.RacecourseId,
                RaceNumber = request.RaceNumber,
                StartTime = request.StartTime,
                TrackLength = request.TrackLength,
                MaxParticipants = request.MaxParticipants,
                Grade = request.Grade.ToString(),
                Status = RaceStatus.Scheduled.ToString(),
                CreateAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };

            await _uow.GetRepository<Race>().AddAsync(race);
            await _uow.SaveAsync();

            race.Racecourse = racecourse;
            race.Tournament = tournament;

            return MapToResponse(race);
        }

        public async Task<RaceResponse> UpdateRaceAsync(Guid raceId, UpdateRaceRequest request)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .Include(r => r.Racecourse)
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            if (race.Status != RaceStatus.Scheduled.ToString())
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
                    .AnyAsync(r => r.TournamentId == race.TournamentId
                        && r.RaceNumber == request.RaceNumber
                        && r.RaceId != raceId
                        && !r.IsDeleted);
                if (duplicate)
                    throw new InvalidOperationException($"Race number {request.RaceNumber} already exists in this tournament.");
                race.RaceNumber = request.RaceNumber;
            }

            if (request.StartTime.HasValue)
            {
                if (request.StartTime.Value <= DateTimeOffset.UtcNow.AddMinutes(30))
                    throw new InvalidOperationException("Start time must be at least 30 minutes from now.");

                Race? latestRace = await _uow.GetRepository<Race>().Entities
                    .Where(r => r.RacecourseId == race.RacecourseId
                        && r.RaceId != raceId
                        && r.Status != "Cancelled"
                        && !r.IsDeleted)
                    .OrderByDescending(r => r.StartTime)
                    .FirstOrDefaultAsync();

                if (latestRace != null)
                {
                    DateTimeOffset latestEnd = (latestRace.EndTime ?? latestRace.StartTime!.Value.AddMinutes(5)).AddMinutes(5);
                    if (request.StartTime.Value < latestEnd)
                        throw new InvalidOperationException($"This racecourse already has a race scheduled. Next race can start after {latestEnd:HH:mm} UTC.");
                }

                race.StartTime = request.StartTime;
            }

            if (request.TrackLength.HasValue)
                race.TrackLength = request.TrackLength;

            if (request.MaxParticipants.HasValue)
                race.MaxParticipants = request.MaxParticipants;

            if (request.Grade != null)
                race.Grade = request.Grade.ToString();

            await _uow.GetRepository<Race>().UpdateAsync(race);
            await _uow.SaveAsync();

            return MapToResponse(race);
        }

        public async Task DeleteRaceAsync(Guid raceId)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            if (race.Status == RaceStatus.Live.ToString())
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
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            if (race.Status != RaceStatus.Scheduled.ToString())
                throw new InvalidOperationException("Horses can only be registered when race is in Scheduled status.");

            Horse? horse = await _uow.GetRepository<Horse>().Entities
                .FirstOrDefaultAsync(h => h.Id == request.HorseId && !h.IsDeleted);
            if (horse == null)
                throw new KeyNotFoundException($"Horse with id {request.HorseId} not found.");

            if (horse.Status != "Active")
                throw new InvalidOperationException("Horse must be in Active status to be registered.");

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
                        && (r.Status == "Confirmed" || r.Status == "Pending"));
                if (activeCount >= race.MaxParticipants.Value)
                    throw new InvalidOperationException($"Race has reached the maximum number of participants ({race.MaxParticipants}).");
            }

            if (request.GateNumber.HasValue)
            {
                bool gateTaken = await _uow.GetRepository<Registration>().Entities
                    .AnyAsync(r => r.RaceId == raceId && r.GateNumber == request.GateNumber);
                if (gateTaken)
                    throw new InvalidOperationException($"Gate number {request.GateNumber} is already taken.");
            }

            bool alreadyRegistered = await _uow.GetRepository<Registration>().Entities
                .AnyAsync(r => r.RaceId == raceId
                    && r.HorseId == request.HorseId
                    && (r.Status == "Pending" || r.Status == "Confirmed"));
            if (alreadyRegistered)
                throw new InvalidOperationException("This horse is already registered in this race.");

            bool ownerAlreadyRegistered = await _uow.GetRepository<Registration>().Entities
                .AnyAsync(r => r.RaceId == raceId
                    && r.Horse.OwnerId == ownerId
                    && (r.Status == "Pending" || r.Status == "Confirmed"));
            if (ownerAlreadyRegistered)
                throw new InvalidOperationException("You have already registered a horse in this race.");

            bool jockeyAlreadyRegistered = await _uow.GetRepository<Registration>().Entities
                .AnyAsync(r => r.RaceId == raceId
                    && r.JockeyId == request.JockeyId
                    && (r.Status == "Pending" || r.Status == "Confirmed"));
            if (jockeyAlreadyRegistered)
                throw new InvalidOperationException("This jockey is already assigned to another horse in this race.");

            Registration registration = new Registration
            {
                RaceId = raceId,
                HorseId = request.HorseId,
                JockeyId = request.JockeyId,
                GateNumber = request.GateNumber,
                OwnerConfirmation = true,
                JockeyConfirmation = null,
                Status = "Pending",
                CreateAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _uow.GetRepository<Registration>().AddAsync(registration);
            await _uow.SaveAsync();

            return new RegistrationResponse
            {
                RegistrationId = registration.RegistrationId,
                JockeyId = registration.JockeyId,
                GateNumber = registration.GateNumber,
                OwnerConfirmation = registration.OwnerConfirmation,
                JockeyConfirmation = registration.JockeyConfirmation,
                Status = registration.Status,
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
                    Status = horse.Status,
                    DerivedStatus = horse.Status
                },
                Race = MapToResponse(race)
            };
        }

        public async Task<PagedResponse<UpcomingRaceResponse>> GetUpcomingRacesAsync(int page, int pageSize, List<string>? statuses)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            List<string> allowedStatuses = (statuses == null || statuses.Count == 0)
                ? new List<string> { RaceStatus.Scheduled.ToString(), RaceStatus.BettingOpen.ToString(), RaceStatus.BettingClosed.ToString() }
                : statuses;

            IGenericRepository<Race> repo = _uow.GetRepository<Race>();

            int totalCount = await repo.Entities
                .CountAsync(r => !r.IsDeleted
                    && allowedStatuses.Contains(r.Status!));

            IEnumerable<UpcomingRaceResponse> items = await repo.FindAsync<UpcomingRaceResponse>(
                predicate: r => !r.IsDeleted
                    && allowedStatuses.Contains(r.Status!),
                orderBy: q => q.OrderBy(r => r.StartTime),
                selector: r => new UpcomingRaceResponse
                {
                    RaceId = r.RaceId,
                    RaceName = "Race " + r.RaceNumber,
                    StartTime = r.StartTime,
                    TrackLength = r.TrackLength,
                    MaxParticipants = r.MaxParticipants,
                    Status = r.Status,
                    TournamentName = r.Tournament.TournamentName,
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
                        HorseName = r.Registration.Horse.HorseName,
                        Breed = r.Registration.Horse.Breed,
                        Color = r.Registration.Horse.Color,
                        Age = r.Registration.Horse.Age,
                        Status = r.Registration.Horse.Status
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
                .Where(r => r.RaceId == raceId && r.Status == "Confirmed")
                .Select(r => new RaceResultHorseDto
                {
                    Id = r.Horse.Id,
                    RegistrationId = r.RegistrationId,
                    HorseName = r.Horse.HorseName,
                    Breed = r.Horse.Breed,
                    Color = r.Horse.Color,
                    Age = r.Horse.Age,
                    Status = r.Horse.Status
                })
                .ToListAsync();
        }

        public async Task ResetRaceAsync(Guid raceId)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .Include(r => r.Racecourse)
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            if (race.Status == "Scheduled")
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
                    if (bet.Status == "Won")
                    {
                        long payout = (long)(bet.BetAmount * (decimal)(bet.PayoutRatio ?? 1));
                        profile.Balance = (profile.Balance ?? 0) - payout + (long)bet.BetAmount;
                    }
                    else if (bet.Status == "Lost" || bet.Status == "Pending")
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

            race.Status = RaceStatus.Scheduled.ToString();
            race.EndTime = null;
            race.StartTime = DateTimeOffset.UtcNow.AddHours(24);
            await _uow.GetRepository<Race>().UpdateAsync(race);

            await _uow.SaveAsync();

            _engine.ClearHorseState(horseIds);
        }

        public async Task<RaceResponse> AdvanceRaceStatusAsync(Guid raceId)
        {
            Race? race = await _uow.GetRepository<Race>().Entities
                .Include(r => r.Racecourse)
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted);

            if (race == null)
                throw new KeyNotFoundException($"Race with id {raceId} not found.");

            string? nextStatus = race.Status switch
            {
                "Scheduled" => "BettingOpen",
                "BettingOpen" => "BettingClosed",
                "BettingClosed" => "Live",
                _ => null
            };

            if (nextStatus == null)
                throw new InvalidOperationException($"Race is in '{race.Status}' status and cannot be advanced.");

            if (nextStatus == "Live")
            {
                int confirmedCount = await _uow.GetRepository<Registration>().Entities
                    .CountAsync(r => r.RaceId == raceId && r.Status == "Confirmed");

                if (confirmedCount == 0)
                    throw new InvalidOperationException("Race must have at least one confirmed registration before going Live.");

                bool otherRaceLive = await _uow.GetRepository<Race>().Entities
                    .AnyAsync(r => r.RacecourseId == race.RacecourseId
                                && r.RaceId != raceId
                                && r.Status == "Live"
                                && !r.IsDeleted);

                if (otherRaceLive)
                    throw new InvalidOperationException("Another race at the same racecourse is currently Live.");
            }

            race.Status = nextStatus;
            await _uow.GetRepository<Race>().UpdateAsync(race);
            await _uow.SaveAsync();

            return MapToResponse(race);
        }

        private static RaceResponse MapToResponse(Race race) => new RaceResponse
        {
            RaceId = race.RaceId,
            RaceNumber = race.RaceNumber,
            StartTime = race.StartTime,
            TrackLength = race.TrackLength,
            MaxParticipants = race.MaxParticipants,
            Status = race.Status,
            Grade = race.Grade,
            RacecourseName = race.Racecourse?.RacecourseName,
            Location = race.Racecourse?.Location,
            Tournament = race.Tournament == null ? null : new TournamentResponse
            {
                TournamentId = race.Tournament.Id,
                TournamentName = race.Tournament.TournamentName,
                Description = race.Tournament.Description,
                StartDate = race.Tournament.StartDate,
                EndDate = race.Tournament.EndDate,
                Status = race.Tournament.Status,
                FundsPrize = race.Tournament.FundsPrize
            }
        };
    }
}
