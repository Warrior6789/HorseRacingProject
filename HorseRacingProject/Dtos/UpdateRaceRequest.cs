using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Dtos
{
    public class UpdateRaceRequest
    {
        public Guid? RacecourseId { get; set; }

        public int? RaceNumber { get; set; }

        public DateTimeOffset? StartTime { get; set; }

        public float? TrackLength { get; set; }

        public int? MaxParticipants { get; set; }

        public RaceGrade? Grade { get; set; }
    }
}
