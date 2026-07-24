using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class RaceRefereeService : IRaceRefereeService
    {
        private readonly IUnitofWork _uow;
        private static readonly TimeSpan TravelBuffer = TimeSpan.FromHours(1);
        private static readonly TimeSpan EstimatedRaceDuration = TimeSpan.FromHours(2);

        public RaceRefereeService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<RaceRefereeResponse> AssignAsync(Guid raceId, Guid refereeId)
        {
            var race = await _uow.GetRepository<Race>().Entities
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted)
                ?? throw new KeyNotFoundException("Race not found.");

            if (race.Status == RaceStatus.Live || race.Status == RaceStatus.Finished || race.Status == RaceStatus.Cancelled)
                throw new InvalidOperationException($"Cannot assign referee to a race with status '{race.Status}'.");

            var referee = await _uow.GetRepository<Account>().Entities
                .FirstOrDefaultAsync(a => a.Id == refereeId && !a.IsDeleted)
                ?? throw new KeyNotFoundException("Account not found.");

            if (referee.Role != AccountRole.Referee)
                throw new InvalidOperationException("Account is not a Referee.");

            if (referee.Status != AccountStatus.Active)
                throw new InvalidOperationException("Referee account is not active.");

            var otherRaces = await _uow.GetRepository<Race>().Entities
                .Where(r => r.RefereeId == refereeId
                         && r.RaceId != raceId
                         && r.Status != RaceStatus.Finished
                         && r.Status != RaceStatus.Cancelled
                         && !r.IsDeleted)
                .ToListAsync();

            foreach (var other in otherRaces)
            {
                bool sameVenue = other.RacecourseId == race.RacecourseId;

                if (sameVenue)
                {
                    var activeStatuses = new[] { RaceStatus.BettingOpen, RaceStatus.BettingClosed, RaceStatus.Live };
                    if (activeStatuses.Contains(other.Status))
                        throw new InvalidOperationException(
                            $"Referee is currently active in Race #{other.RaceNumber} at the same racecourse.");
                }
                else
                {
                    DateTimeOffset estimatedEnd = other.EndTime
                        ?? other.StartTime!.Value.Add(EstimatedRaceDuration);

                    if (race.StartTime.HasValue && estimatedEnd.Add(TravelBuffer) > race.StartTime.Value)
                        throw new InvalidOperationException(
                            $"Referee cannot travel in time: Race #{other.RaceNumber} ends around " +
                            $"{estimatedEnd:HH:mm}, plus 1h travel exceeds start time {race.StartTime.Value:HH:mm}.");
                }
            }

            race.RefereeId = refereeId;
            await _uow.GetRepository<Race>().UpdateAsync(race);
            await _uow.SaveAsync();

            var profile = await _uow.GetRepository<UserProfile>().Entities
                .FirstOrDefaultAsync(p => p.AccountId == refereeId && !p.IsDeleted);

            return new RaceRefereeResponse
            {
                RaceId       = raceId,
                RefereeId    = refereeId,
                RefereeName  = profile?.FullName ?? referee.Email,
                RefereeEmail = referee.Email
            };
        }

        public async Task UnassignAsync(Guid raceId)
        {
            var race = await _uow.GetRepository<Race>().Entities
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted)
                ?? throw new KeyNotFoundException("Race not found.");

            if (race.Status == RaceStatus.Live)
                throw new InvalidOperationException("Cannot unassign referee while the race is Live.");

            if (race.RefereeId == null)
                throw new InvalidOperationException("No referee is assigned to this race.");

            race.RefereeId = null;
            await _uow.GetRepository<Race>().UpdateAsync(race);
            await _uow.SaveAsync();
        }

        public async Task<RaceRefereeResponse?> GetByRaceAsync(Guid raceId)
        {
            var race = await _uow.GetRepository<Race>().Entities
                .Include(r => r.Referee)
                    .ThenInclude(a => a!.UserProfile)
                .FirstOrDefaultAsync(r => r.RaceId == raceId && !r.IsDeleted)
                ?? throw new KeyNotFoundException("Race not found.");

            if (race.Referee == null) return null;

            return new RaceRefereeResponse
            {
                RaceId       = raceId,
                RefereeId    = race.Referee.Id,
                RefereeName  = (race.Referee.UserProfile != null ? race.Referee.UserProfile.FullName : null) ?? race.Referee.Email,
                RefereeEmail = race.Referee.Email
            };
        }

        public async Task<List<RaceResponse>> GetMyAssignedRacesAsync(Guid refereeId)
        {
            return await _uow.GetRepository<Race>().Entities
                .Include(r => r.Racecourse)
                .Where(r => r.RefereeId == refereeId && !r.IsDeleted)
                .OrderBy(r => r.StartTime)
                .Select(r => new RaceResponse
                {
                    RaceId              = r.RaceId,
                    RaceNumber          = r.RaceNumber,
                    RaceName            = r.RaceName,
                    StartTime           = r.StartTime,
                    TrackLength         = r.TrackLength,
                    MaxParticipants     = r.MaxParticipants,
                    Status              = r.Status.ToString(),
                    RegistrationFee     = r.RegistrationFee,
                    PrizePool           = r.PrizePool,
                    RacecourseName      = r.Racecourse.RacecourseName,
                    Location            = r.Racecourse.Location,
                    ImageUrl            = r.ImageUrl,
                    RegistrationCount   = r.Registrations.Count()
                })
                .ToListAsync();
        }
    }
}
