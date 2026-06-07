using HorseRacingAPI.Dtos;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly IUnitofWork _uow;
        public RegistrationService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task AcceptRegistrationAsync(Guid registrationId, Guid jockeyAccountId)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();

            Registration? registration = await registrationRepo.Entities.FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
            if(registration == null)
            {
                throw new ArgumentException("Registration not found.");
            }
            if(registration.JockeyId != jockeyAccountId)
            {
                throw new InvalidOperationException("You are not authorized to accept this registration.");
            }
            if(registration.Status != "Pending")
            {
                throw new InvalidOperationException("Only pending registrations can be accepted.");
            }
            registration.JockeyConfirmation = true;
            registration.Status = "Confirmed";
            registration.UpdatedAt = DateTimeOffset.UtcNow;

            await _uow.SaveAsync();
        }

        public async Task<List<RegistrationResponse>> GetMyRequestAsync(Guid jockeyAccountId)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();
            List<RegistrationResponse>? registrations = await registrationRepo.Entities
                .Include(r => r.Horse)
                .ThenInclude(r => r.Owner)
                .Include(r => r.Race)
                .ThenInclude(r => r.Racecourse)
                .Include(r => r.Race)
                .ThenInclude(r => r.Tournament)
                .Where(j => j.JockeyId == jockeyAccountId
                && j.Status == "Pending"
                && j.OwnerConfirmation == true
                && j.JockeyConfirmation == false
                )
                .Select(r => new RegistrationResponse
                {
                    RegistrationId = r.RegistrationId,
                    JockeyId = r.JockeyId,
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
                        Status = r.Horse.Status
                    },
                    Race = new RaceResponse
                    {
                        RaceId = r.RaceId,
                        RaceNumber = r.Race.RaceNumber,
                        StartTime = r.Race.StartTime,
                        TrackLength = r.Race.TrackLength,
                        MaxParticipants = r.Race.MaxParticipants,
                        Status = r.Race.Status,
                        RacecourseName = r.Race.Racecourse.RacecourseName,
                        Location = r.Race.Racecourse.Location,
                        Tournament = new TournamentResponse
                        {
                            TournamentId = r.Race.Tournament.Id,
                            TournamentName = r.Race.Tournament.TournamentName,
                            Description = r.Race.Tournament.Description,
                            StartDate = r.Race.Tournament.StartDate,
                            EndDate = r.Race.Tournament.EndDate,
                            Status = r.Race.Tournament.Status
                        }
                    },
                    OwnerConfirmation = r.OwnerConfirmation,
                    JockeyConfirmation = r.JockeyConfirmation,
                    Status = r.Status,
                    CreateAt = r.CreateAt,
                    UpdatedAt = r.UpdatedAt
                }).ToListAsync();
               return registrations;
        }

        public async Task<PagedResponse<RegistrationResponse>> GetMyRequestPagedAsync(Guid jockeyAccountId, int page, int pageSize)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();

            int totalCount = await registrationRepo.Entities
                .Where(j => j.JockeyId == jockeyAccountId
                    && j.Status == "Pending"
                    && j.OwnerConfirmation == true
                    && j.JockeyConfirmation == false)
                .CountAsync();

            IEnumerable<RegistrationResponse> items = await registrationRepo.FindAsync<RegistrationResponse>(
                predicate: j => j.JockeyId == jockeyAccountId
                    && j.Status == "Pending"
                    && j.OwnerConfirmation == true
                    && j.JockeyConfirmation == false,
                orderBy: null,
                selector: r => new RegistrationResponse
                {
                    RegistrationId = r.RegistrationId,
                    JockeyId = r.JockeyId,
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
                        Status = r.Horse.Status
                    },
                    Race = new RaceResponse
                    {
                        RaceId = r.RaceId,
                        RaceNumber = r.Race.RaceNumber,
                        StartTime = r.Race.StartTime,
                        TrackLength = r.Race.TrackLength,
                        MaxParticipants = r.Race.MaxParticipants,
                        Status = r.Race.Status,
                        RacecourseName = r.Race.Racecourse.RacecourseName,
                        Location = r.Race.Racecourse.Location,
                        Tournament = new TournamentResponse
                        {
                            TournamentId = r.Race.Tournament.Id,
                            TournamentName = r.Race.Tournament.TournamentName,
                            Description = r.Race.Tournament.Description,
                            StartDate = r.Race.Tournament.StartDate,
                            EndDate = r.Race.Tournament.EndDate,
                            Status = r.Race.Tournament.Status
                        }
                    },
                    OwnerConfirmation = r.OwnerConfirmation,
                    JockeyConfirmation = r.JockeyConfirmation,
                    Status = r.Status,
                    CreateAt = r.CreateAt,
                    UpdatedAt = r.UpdatedAt
                },
                pageIndex: page - 1,
                pageSize: pageSize
            );

            return new PagedResponse<RegistrationResponse>
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task RejectRegistrationAsync(Guid registrationId, Guid jockeyAccountId)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();

            Registration? registration = await registrationRepo.Entities.FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
            if (registration == null)
            {
                throw new ArgumentException("Registration not found.");
            }
            if (registration.JockeyId != jockeyAccountId)
            {
                throw new InvalidOperationException("You are not authorized to reject this registration.");
            }
            if (registration.Status != "Pending")
            {
                throw new InvalidOperationException("Only pending registrations can be rejected.");
            }
            registration.JockeyConfirmation = false;
            registration.Status = "Rejected";
            registration.UpdatedAt = DateTimeOffset.UtcNow;

            await _uow.SaveAsync();
        }
    }
}
