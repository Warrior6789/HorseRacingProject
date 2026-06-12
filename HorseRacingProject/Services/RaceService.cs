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

public RaceService(IUnitofWork uow)
        {
            _uow = uow;
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
                    RacecourseName = r.Racecourse.RacecourseName,
                    Location = r.Racecourse.Location,
                    Tournament = new TournamentResponse
                    {
                        TournamentId = r.Tournament.Id,
                        TournamentName = r.Tournament.TournamentName,
                        Description = r.Tournament.Description,
                        StartDate = r.Tournament.StartDate,
                        EndDate = r.Tournament.EndDate,
                        Status = r.Tournament.Status
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
                    RacecourseName = r.Racecourse.RacecourseName,
                    Location = r.Racecourse.Location,
                    Tournament = new TournamentResponse
                    {
                        TournamentId = r.Tournament.Id,
                        TournamentName = r.Tournament.TournamentName,
                        Description = r.Tournament.Description,
                        StartDate = r.Tournament.StartDate,
                        EndDate = r.Tournament.EndDate,
                        Status = r.Tournament.Status
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

            bool duplicate = await _uow.GetRepository<Race>().Entities
                .AnyAsync(r => r.TournamentId == request.TournamentId
                    && r.RaceNumber == request.RaceNumber
                    && !r.IsDeleted);
            if (duplicate)
                throw new InvalidOperationException($"Race number {request.RaceNumber} already exists in this tournament.");

            Race race = new Race
            {
                TournamentId = request.TournamentId,
                RacecourseId = request.RacecourseId,
                RaceNumber = request.RaceNumber,
                StartTime = request.StartTime,
                TrackLength = request.TrackLength,
                MaxParticipants = request.MaxParticipants,
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
                race.StartTime = request.StartTime;

            if (request.TrackLength.HasValue)
                race.TrackLength = request.TrackLength;

            if (request.MaxParticipants.HasValue)
                race.MaxParticipants = request.MaxParticipants;

            race.UpdatedAt = DateTimeOffset.UtcNow;
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

            if (horse.OwnerId != ownerId)
                throw new ForbiddenAccessException("You do not own this horse.");

            Account? jockey = await _uow.GetRepository<Account>().Entities
                .FirstOrDefaultAsync(a => a.Id == request.JockeyId && !a.IsDeleted);
            if (jockey == null)
                throw new KeyNotFoundException($"Jockey with id {request.JockeyId} not found.");

            if (jockey.Role != AccountRole.Jockey)
                throw new InvalidOperationException("The specified account is not a Jockey.");

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
                    Status = horse.Status
                },
                Race = MapToResponse(race)
            };
        }

        private static RaceResponse MapToResponse(Race race) => new RaceResponse
        {
            RaceId = race.RaceId,
            RaceNumber = race.RaceNumber,
            StartTime = race.StartTime,
            TrackLength = race.TrackLength,
            MaxParticipants = race.MaxParticipants,
            Status = race.Status,
            RacecourseName = race.Racecourse?.RacecourseName,
            Location = race.Racecourse?.Location,
            Tournament = race.Tournament == null ? null : new TournamentResponse
            {
                TournamentId = race.Tournament.Id,
                TournamentName = race.Tournament.TournamentName,
                Description = race.Tournament.Description,
                StartDate = race.Tournament.StartDate,
                EndDate = race.Tournament.EndDate,
                Status = race.Tournament.Status
            }
        };
    }
}
