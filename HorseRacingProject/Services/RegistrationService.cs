using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HorseRacingAPI.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly IUnitofWork _uow;
        private readonly IHubContext<RaceHub> _hubContext;

        public RegistrationService(IUnitofWork uow, IHubContext<RaceHub> hubContext)
        {
            _uow = uow;
            _hubContext = hubContext;
        }

        private async Task<object> GetRegistrationKpiAsync()
        {
            var repo = _uow.GetRepository<Registration>();
            int pendingCount  = await repo.Entities.CountAsync(r => r.Status == RegistrationStatus.Pending);
            int approvedCount = await repo.Entities.CountAsync(r => r.Status == RegistrationStatus.Confirmed);
            int rejectedCount = await repo.Entities.CountAsync(r => r.Status == RegistrationStatus.Rejected);
            return new { pendingCount, approvedCount, rejectedCount };
        }

        public async Task AcceptRegistrationAsync(Guid registrationId, Guid jockeyAccountId)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();

            Registration? registration = await registrationRepo.Entities
                .Include(r => r.Race)
                .Include(r => r.Horse)
                .FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
            if (registration == null)
                throw new ArgumentException("Registration not found.");

            if (registration.JockeyId != jockeyAccountId)
                throw new InvalidOperationException("You are not authorized to accept this registration.");

            if (registration.Status != RegistrationStatus.Pending)
                throw new InvalidOperationException("Only pending registrations can be accepted.");

            if (registration.Race.MaxParticipants.HasValue)
            {
                int confirmedCount = await registrationRepo.Entities
                    .CountAsync(r => r.RaceId == registration.RaceId && r.Status == RegistrationStatus.Confirmed);
                if (confirmedCount >= registration.Race.MaxParticipants.Value)
                    throw new InvalidOperationException($"Race has reached the maximum number of participants ({registration.Race.MaxParticipants}).");
            }

            bool jockeyAlreadyInSameRace = await _uow.GetRepository<Registration>().Entities
                .AnyAsync(r => r.JockeyId == jockeyAccountId
                    && r.RegistrationId != registrationId
                    && r.RaceId == registration.RaceId
                    && r.Status == RegistrationStatus.Confirmed);
            if (jockeyAlreadyInSameRace)
                throw new InvalidOperationException("You are already confirmed in this race with another horse.");

            var confirmedElsewhere = await _uow.GetRepository<Registration>().Entities
                .Where(r => r.JockeyId == jockeyAccountId
                    && r.RegistrationId != registrationId
                    && r.RaceId != registration.RaceId
                    && r.Status == RegistrationStatus.Confirmed)
                .Select(r => new { r.Race.StartTime, r.Race.EndTime, r.Race.RacecourseId })
                .ToListAsync();

            foreach (var other in confirmedElsewhere)
            {
                bool sameVenue = other.RacecourseId == registration.Race.RacecourseId;
                DateTimeOffset raceStart = registration.Race.StartTime!.Value;
                DateTimeOffset raceEnd   = registration.Race.EndTime ?? registration.Race.StartTime!.Value.AddMinutes(5);
                DateTimeOffset otherEnd  = other.EndTime ?? other.StartTime!.Value.AddMinutes(5);

                DateTimeOffset effectiveStart = sameVenue ? raceStart : raceStart.AddHours(-2);
                DateTimeOffset effectiveEnd   = sameVenue ? raceEnd   : raceEnd.AddHours(2);

                if (other.StartTime < effectiveEnd && otherEnd > effectiveStart)
                    throw new InvalidOperationException("You are already confirmed in another race that conflicts in time.");
            }

            await _uow.BeginTransactionAsync();
            try
            {
                registration.JockeyConfirmation = true;
                registration.Status = RegistrationStatus.Confirmed;
                registration.UpdatedAt = DateTimeOffset.UtcNow;
                await _uow.SaveAsync();

                List<Registration> otherHorsePending = await _uow.GetRepository<Registration>().Entities
                    .Include(r => r.Race)
                    .Include(r => r.Horse)
                    .Where(r => r.HorseId == registration.HorseId
                        && r.RegistrationId != registrationId
                        && r.Status == RegistrationStatus.Pending)
                    .ToListAsync();

                foreach (Registration other in otherHorsePending)
                {
                    other.JockeyConfirmation = false;
                    other.Status = RegistrationStatus.Rejected;
                    other.UpdatedAt = DateTimeOffset.UtcNow;
                    await _uow.GetRepository<Registration>().UpdateAsync(other);

                    if (other.Race.RegistrationFee > 0)
                    {
                        UserProfile? ownerProfile = await _uow.GetRepository<UserProfile>().Entities
                            .FirstOrDefaultAsync(p => p.AccountId == other.Horse.OwnerId && !p.IsDeleted);
                        if (ownerProfile != null)
                        {
                            ownerProfile.Balance = (ownerProfile.Balance ?? 0) + (long)other.Race.RegistrationFee;
                            ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                            await _uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);
                        }
                        other.Race.PrizePool = Math.Max(0, other.Race.PrizePool - other.Race.RegistrationFee);
                        await _uow.GetRepository<Race>().UpdateAsync(other.Race);
                    }
                }

                await _uow.SaveAsync();
                await _uow.CommitTransactionAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "P0001")
            {
                await _uow.RollbackTransactionAsync();
                throw new InvalidOperationException($"Race has reached the maximum number of participants ({registration.Race.MaxParticipants}).", ex);
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<List<RegistrationResponse>> GetMyRequestAsync(Guid jockeyAccountId)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();
            List<RegistrationResponse>? registrations = await registrationRepo.Entities
                .Include(r => r.Horse)
                .ThenInclude(r => r.Owner)
                .Include(r => r.Race)
                .ThenInclude(r => r.Racecourse)
                .Where(j => j.JockeyId == jockeyAccountId
                    && j.Status == RegistrationStatus.Pending
                    && j.OwnerConfirmation == true
                    && j.JockeyConfirmation == null)
                .Select(r => new RegistrationResponse
                {
                    RegistrationId = r.RegistrationId,
                    JockeyId = r.JockeyId,
                    GateNumber = r.GateNumber,
                    Horse = new HorseResponse
                    {
                        Id = r.HorseId,
                        OwnerId = r.Horse.OwnerId,
                        OwnerName = r.Horse.Owner.UserProfiles.Select(up => up.FullName).FirstOrDefault(),
                        HorseName = r.Horse.HorseName,
                        Breed = r.Horse.Breed,
                        Color = r.Horse.Color,
                        Age = r.Horse.Age,
                        Weight = r.Horse.Weight,
                        RecordWins = r.Horse.RecordWins,
                        Status = r.Horse.Status.ToString(),
                        DerivedStatus = r.Horse.Status.ToString()
                    },
                    Race = new RaceResponse
                    {
                        RaceId = r.RaceId,
                        RaceNumber = r.Race.RaceNumber,
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
                }).ToListAsync();
            return registrations;
        }

        public async Task<PagedResponse<RegistrationResponse>> GetMyRequestPagedAsync(Guid jockeyAccountId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();

            int totalCount = await registrationRepo.Entities
                .Where(j => j.JockeyId == jockeyAccountId
                    && j.Status == RegistrationStatus.Pending
                    && j.OwnerConfirmation == true
                    && j.JockeyConfirmation == null)
                .CountAsync();

            IEnumerable<RegistrationResponse> items = await registrationRepo.FindAsync<RegistrationResponse>(
                predicate: j => j.JockeyId == jockeyAccountId
                    && j.Status == RegistrationStatus.Pending
                    && j.OwnerConfirmation == true
                    && j.JockeyConfirmation == null,
                orderBy: null,
                selector: r => new RegistrationResponse
                {
                    RegistrationId = r.RegistrationId,
                    JockeyId = r.JockeyId,
                    GateNumber = r.GateNumber,
                    Horse = new HorseResponse
                    {
                        Id = r.HorseId,
                        OwnerId = r.Horse.OwnerId,
                        OwnerName = r.Horse.Owner.UserProfiles.Select(up => up.FullName).FirstOrDefault(),
                        HorseName = r.Horse.HorseName,
                        Breed = r.Horse.Breed,
                        Color = r.Horse.Color,
                        Age = r.Horse.Age,
                        Weight = r.Horse.Weight,
                        RecordWins = r.Horse.RecordWins,
                        Status = r.Horse.Status.ToString(),
                        DerivedStatus = r.Horse.Status.ToString()
                    },
                    Race = new RaceResponse
                    {
                        RaceId = r.RaceId,
                        RaceNumber = r.Race.RaceNumber,
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

        public async Task<List<RegistrationResponse>> GetOwnerRequestAsync(Guid ownerAccountId)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();
            return await registrationRepo.Entities
                .Include(r => r.Horse)
                .ThenInclude(r => r.Owner)
                .Include(r => r.Race)
                .ThenInclude(r => r.Racecourse)
                .Where(r => r.Horse.OwnerId == ownerAccountId
                    && r.Status == RegistrationStatus.Pending)
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
                        Status = r.Horse.Status.ToString(),
                        DerivedStatus = r.Horse.Status.ToString()
                    },
                    Race = new RaceResponse
                    {
                        RaceId = r.RaceId,
                        RaceNumber = r.Race.RaceNumber,
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
                }).ToListAsync();
        }

        public async Task<PagedResponse<RegistrationResponse>> GetOwnerRequestPagedAsync(Guid ownerAccountId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();

            int totalCount = await registrationRepo.Entities
                .Where(r => r.Horse.OwnerId == ownerAccountId
                    && r.Status == RegistrationStatus.Pending)
                .CountAsync();

            IEnumerable<RegistrationResponse> items = await registrationRepo.FindAsync<RegistrationResponse>(
                predicate: r => r.Horse.OwnerId == ownerAccountId
                    && r.Status == RegistrationStatus.Pending,
                orderBy: null,
                selector: r => new RegistrationResponse
                {
                    RegistrationId = r.RegistrationId,
                    JockeyId = r.JockeyId,
                    GateNumber = r.GateNumber,
                    Horse = new HorseResponse
                    {
                        Id = r.HorseId,
                        OwnerId = r.Horse.OwnerId,
                        OwnerName = r.Horse.Owner.UserProfiles.Select(up => up.FullName).FirstOrDefault(),
                        HorseName = r.Horse.HorseName,
                        Breed = r.Horse.Breed,
                        Color = r.Horse.Color,
                        Age = r.Horse.Age,
                        Weight = r.Horse.Weight,
                        RecordWins = r.Horse.RecordWins,
                        Status = r.Horse.Status.ToString(),
                        DerivedStatus = r.Horse.Status.ToString()
                    },
                    Race = new RaceResponse
                    {
                        RaceId = r.RaceId,
                        RaceNumber = r.Race.RaceNumber,
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

        public async Task<List<RegistrationResponse>> GetAllOwnerRegistrationsAsync(Guid ownerAccountId)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();
            return await registrationRepo.Entities
                .Include(r => r.Horse)
                .Include(r => r.Race)
                .ThenInclude(r => r.Racecourse)
                .Where(r => r.Horse.OwnerId == ownerAccountId)
                .OrderByDescending(r => r.CreateAt)
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
                        Status = r.Horse.Status.ToString(),
                        DerivedStatus = r.Horse.Status.ToString()
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
                }).ToListAsync();
        }

        public async Task<PagedResponse<RegistrationResponse>> GetAllOwnerRegistrationsPagedAsync(Guid ownerAccountId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<Registration> repo = _uow.GetRepository<Registration>();

            IQueryable<Registration> query = repo.Entities
                .Include(r => r.Horse)
                .Include(r => r.Race).ThenInclude(r => r.Racecourse)
                .Where(r => r.Horse.OwnerId == ownerAccountId)
                .OrderByDescending(r => r.CreateAt);

            int total = await query.CountAsync();

            List<RegistrationResponse> items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RegistrationResponse
                {
                    RegistrationId    = r.RegistrationId,
                    JockeyId          = r.JockeyId,
                    GateNumber        = r.GateNumber,
                    OwnerConfirmation = r.OwnerConfirmation,
                    JockeyConfirmation = r.JockeyConfirmation,
                    Status            = r.Status.ToString(),
                    CreateAt          = r.CreateAt,
                    UpdatedAt         = r.UpdatedAt,
                    Jockey = r.Jockey.JockeyProfiles
                        .Where(jp => !jp.IsDeleted)
                        .Select(jp => new JockeyProfileResponse
                        {
                            JockeyProfileId    = jp.JockeyProfileId,
                            AccountId          = jp.AccountId,
                            FullName           = jp.FullName,
                            DateOfBirth        = jp.DateOfBirth,
                            Nationality        = jp.Nationality,
                            LicenseNumber      = jp.LicenseNumber,
                            Weight             = jp.Weight,
                            Height             = jp.Height,
                            TotalRaces         = jp.TotalRaces,
                            TotalWins          = jp.TotalWins,
                            ImageUrl           = jp.ImageUrl,
                            CertificateImageUrl = jp.CertificateImageUrl,
                            CreateAt           = jp.CreateAt,
                            UpdatedAt          = jp.UpdatedAt
                        })
                        .FirstOrDefault(),
                    Horse = new HorseResponse
                    {
                        Id            = r.HorseId,
                        HorseName     = r.Horse.HorseName,
                        Breed         = r.Horse.Breed,
                        Color         = r.Horse.Color,
                        Age           = r.Horse.Age,
                        Weight        = r.Horse.Weight,
                        RecordWins    = r.Horse.RecordWins,
                        Status        = r.Horse.Status.ToString(),
                        DerivedStatus = r.Horse.Status.ToString()
                    },
                    Race = new RaceResponse
                    {
                        RaceId          = r.RaceId,
                        RaceNumber      = r.Race.RaceNumber,
                        RaceName        = r.Race.RaceName,
                        StartTime       = r.Race.StartTime,
                        TrackLength     = r.Race.TrackLength,
                        MaxParticipants = r.Race.MaxParticipants,
                        Status          = r.Race.Status.ToString(),
                        RacecourseName  = r.Race.Racecourse.RacecourseName,
                        Location        = r.Race.Racecourse.Location
                    }
                })
                .ToListAsync();

            return new PagedResponse<RegistrationResponse>
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = total
            };
        }

        public async Task RejectRegistrationAsync(Guid registrationId, Guid jockeyAccountId)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();

            Registration? registration = await registrationRepo.Entities
                .Include(r => r.Race)
                .Include(r => r.Horse)
                .FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
            if (registration == null)
                throw new ArgumentException("Registration not found.");

            if (registration.JockeyId != jockeyAccountId)
                throw new InvalidOperationException("You are not authorized to reject this registration.");

            if (registration.Status != RegistrationStatus.Pending)
                throw new InvalidOperationException("Only pending registrations can be rejected.");

            registration.JockeyConfirmation = false;
            registration.Status = RegistrationStatus.Rejected;
            registration.UpdatedAt = DateTimeOffset.UtcNow;

            if (registration.Race.RegistrationFee > 0)
            {
                UserProfile? ownerProfile = await _uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == registration.Horse.OwnerId && !p.IsDeleted);
                if (ownerProfile != null)
                {
                    ownerProfile.Balance = (ownerProfile.Balance ?? 0) + (long)registration.Race.RegistrationFee;
                    ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                    await _uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);
                }
                registration.Race.PrizePool = Math.Max(0, registration.Race.PrizePool - registration.Race.RegistrationFee);
                await _uow.GetRepository<Race>().UpdateAsync(registration.Race);
            }

            await _uow.SaveAsync();
        }

        public async Task AdminAcceptRegistrationAsync(Guid registrationId)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();

            Registration? registration = await registrationRepo.Entities
                .Include(r => r.Race)
                .Include(r => r.Horse)
                .FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
            if (registration == null)
                throw new ArgumentException("Registration not found.");

            if (registration.Status != RegistrationStatus.Pending)
                throw new InvalidOperationException("Only pending registrations can be accepted.");

            if (registration.Race.MaxParticipants.HasValue)
            {
                int confirmedCount = await registrationRepo.Entities
                    .CountAsync(r => r.RaceId == registration.RaceId && r.Status == RegistrationStatus.Confirmed);
                if (confirmedCount >= registration.Race.MaxParticipants.Value)
                    throw new InvalidOperationException($"Race has reached the maximum number of participants ({registration.Race.MaxParticipants}).");
            }

            bool jockeyAlreadyInSameRace = await _uow.GetRepository<Registration>().Entities
                .AnyAsync(r => r.JockeyId == registration.JockeyId
                    && r.RegistrationId != registrationId
                    && r.RaceId == registration.RaceId
                    && r.Status == RegistrationStatus.Confirmed);
            if (jockeyAlreadyInSameRace)
                throw new InvalidOperationException("This jockey is already confirmed in this race with another horse.");

            var confirmedElsewhere = await _uow.GetRepository<Registration>().Entities
                .Where(r => r.JockeyId == registration.JockeyId
                    && r.RegistrationId != registrationId
                    && r.RaceId != registration.RaceId
                    && r.Status == RegistrationStatus.Confirmed)
                .Select(r => new { r.Race.StartTime, r.Race.EndTime, r.Race.RacecourseId })
                .ToListAsync();

            foreach (var other in confirmedElsewhere)
            {
                bool sameVenue = other.RacecourseId == registration.Race.RacecourseId;
                DateTimeOffset raceStart = registration.Race.StartTime!.Value;
                DateTimeOffset raceEnd   = registration.Race.EndTime ?? registration.Race.StartTime!.Value.AddMinutes(5);
                DateTimeOffset otherEnd  = other.EndTime ?? other.StartTime!.Value.AddMinutes(5);

                DateTimeOffset effectiveStart = sameVenue ? raceStart : raceStart.AddHours(-2);
                DateTimeOffset effectiveEnd   = sameVenue ? raceEnd   : raceEnd.AddHours(2);

                if (other.StartTime < effectiveEnd && otherEnd > effectiveStart)
                    throw new InvalidOperationException("This jockey is already confirmed in another race that conflicts in time.");
            }

            await _uow.BeginTransactionAsync();
            try
            {
                registration.JockeyConfirmation = true;
                registration.Status = RegistrationStatus.Confirmed;
                registration.UpdatedAt = DateTimeOffset.UtcNow;
                await _uow.SaveAsync();

                List<Registration> otherHorsePending = await _uow.GetRepository<Registration>().Entities
                    .Include(r => r.Race)
                    .Include(r => r.Horse)
                    .Where(r => r.HorseId == registration.HorseId
                        && r.RegistrationId != registrationId
                        && r.Status == RegistrationStatus.Pending)
                    .ToListAsync();

                foreach (Registration other in otherHorsePending)
                {
                    other.JockeyConfirmation = false;
                    other.Status = RegistrationStatus.Rejected;
                    other.UpdatedAt = DateTimeOffset.UtcNow;
                    await _uow.GetRepository<Registration>().UpdateAsync(other);

                    if (other.Race.RegistrationFee > 0)
                    {
                        UserProfile? ownerProfile = await _uow.GetRepository<UserProfile>().Entities
                            .FirstOrDefaultAsync(p => p.AccountId == other.Horse.OwnerId && !p.IsDeleted);
                        if (ownerProfile != null)
                        {
                            ownerProfile.Balance = (ownerProfile.Balance ?? 0) + (long)other.Race.RegistrationFee;
                            ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                            await _uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);
                        }
                        other.Race.PrizePool = Math.Max(0, other.Race.PrizePool - other.Race.RegistrationFee);
                        await _uow.GetRepository<Race>().UpdateAsync(other.Race);
                    }
                }

                await _uow.SaveAsync();
                await _uow.CommitTransactionAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "P0001")
            {
                await _uow.RollbackTransactionAsync();
                throw new InvalidOperationException($"Race has reached the maximum number of participants ({registration.Race.MaxParticipants}).", ex);
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }

            await _hubContext.Clients.All.SendAsync("RegistrationsUpdated", await GetRegistrationKpiAsync());
        }

        public async Task AdminRejectRegistrationAsync(Guid registrationId)
        {
            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();

            Registration? registration = await registrationRepo.Entities
                .Include(r => r.Race)
                .Include(r => r.Horse)
                .FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
            if (registration == null)
                throw new ArgumentException("Registration not found.");

            if (registration.Status != RegistrationStatus.Pending)
                throw new InvalidOperationException("Only pending registrations can be rejected.");

            registration.JockeyConfirmation = false;
            registration.Status = RegistrationStatus.Rejected;
            registration.UpdatedAt = DateTimeOffset.UtcNow;

            if (registration.Race.RegistrationFee > 0)
            {
                UserProfile? ownerProfile = await _uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == registration.Horse.OwnerId && !p.IsDeleted);
                if (ownerProfile != null)
                {
                    ownerProfile.Balance = (ownerProfile.Balance ?? 0) + (long)registration.Race.RegistrationFee;
                    ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                    await _uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);
                }
                registration.Race.PrizePool = Math.Max(0, registration.Race.PrizePool - registration.Race.RegistrationFee);
                await _uow.GetRepository<Race>().UpdateAsync(registration.Race);
            }

            await _uow.SaveAsync();

            await _hubContext.Clients.All.SendAsync("RegistrationsUpdated", await GetRegistrationKpiAsync());
        }

        public async Task<PagedResponse<RegistrationResponse>> GetAllRegistrationsPagedAsync(int page, int pageSize, Guid? raceId = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<Registration> registrationRepo = _uow.GetRepository<Registration>();

            IQueryable<Registration> baseQuery = registrationRepo.Entities
                .Where(r => raceId == null || r.RaceId == raceId);

            int totalCount = await baseQuery.CountAsync();

            List<RegistrationResponse> items = await baseQuery
                .Include(r => r.Horse).ThenInclude(r => r.Owner)
                .Include(r => r.Race).ThenInclude(r => r.Racecourse)
                .OrderByDescending(r => r.CreateAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                        Status = r.Horse.Status.ToString(),
                        DerivedStatus = r.Horse.Status.ToString()
                    },
                    Race = new RaceResponse
                    {
                        RaceId = r.RaceId,
                        RaceNumber = r.Race.RaceNumber,
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
                }).ToListAsync();

            return new PagedResponse<RegistrationResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task ScratchHorseAsync(Guid registrationId)
        {
            Registration? registration = await _uow.GetRepository<Registration>().Entities
                .Include(r => r.Race)
                .Include(r => r.Horse)
                .FirstOrDefaultAsync(r => r.RegistrationId == registrationId);

            if (registration == null)
                throw new KeyNotFoundException("Registration not found.");

            if (registration.Status != RegistrationStatus.Confirmed)
                throw new InvalidOperationException("Only confirmed registrations can be scratched.");

            RaceStatus raceStatus = registration.Race.Status;
            if (raceStatus == RaceStatus.Live || raceStatus == RaceStatus.Finished || raceStatus == RaceStatus.Cancelled)
                throw new InvalidOperationException($"Cannot scratch a horse when race is {raceStatus}.");

            await _uow.BeginTransactionAsync();
            try
            {
                registration.Status = RegistrationStatus.Scratched;
                registration.UpdatedAt = DateTimeOffset.UtcNow;
                await _uow.GetRepository<Registration>().UpdateAsync(registration);

                if (registration.Race.RegistrationFee > 0)
                {
                    UserProfile? ownerProfile = await _uow.GetRepository<UserProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == registration.Horse.OwnerId && !p.IsDeleted);
                    if (ownerProfile != null)
                    {
                        ownerProfile.Balance = (ownerProfile.Balance ?? 0) + (long)registration.Race.RegistrationFee;
                        ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                        await _uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);
                    }
                    registration.Race.PrizePool = Math.Max(0, registration.Race.PrizePool - registration.Race.RegistrationFee);
                    await _uow.GetRepository<Race>().UpdateAsync(registration.Race);
                }

                List<Bet> bets = await _uow.GetRepository<Bet>().Entities
                    .Where(b => b.RegistrationId == registrationId && b.Status == BetStatus.Active)
                    .ToListAsync();

                foreach (Bet bet in bets)
                {
                    bet.Status = BetStatus.Refunded;
                    await _uow.GetRepository<Bet>().UpdateAsync(bet);

                    UserProfile? profile = await _uow.GetRepository<UserProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == bet.SpectatorId && !p.IsDeleted);
                    if (profile != null)
                    {
                        profile.Balance = (profile.Balance ?? 0) + (long)bet.BetAmount;
                        profile.UpdatedAt = DateTimeOffset.UtcNow;
                        await _uow.GetRepository<UserProfile>().UpdateAsync(profile);
                    }
                }

                var grouped = bets.GroupBy(b => b.BetType);
                foreach (var group in grouped)
                {
                    decimal totalRefunded = group.Sum(b => b.BetAmount);
                    await _uow.GetRepository<RacePool>().Entities
                        .Where(p => p.RaceId == registration.RaceId && p.BetType == group.Key)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.TotalAmount,
                            p => p.TotalAmount - totalRefunded));
                }

                await _uow.SaveAsync();
                await _uow.CommitTransactionAsync();
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }

            await _hubContext.Clients.All.SendAsync("RegistrationsUpdated", await GetRegistrationKpiAsync());
        }
    }
}
