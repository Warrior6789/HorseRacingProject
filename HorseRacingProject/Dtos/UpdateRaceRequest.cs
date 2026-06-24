using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class UpdateRaceRequest
    {
        public Guid? RacecourseId { get; set; }

        public int? RaceNumber { get; set; }

        [MaxLength(100)]
        public string? RaceName { get; set; }

        public DateTimeOffset? StartTime { get; set; }

        public float? TrackLength { get; set; }

        [Range(3, int.MaxValue, ErrorMessage = "MaxParticipants must be at least 3.")]
        public int? MaxParticipants { get; set; }
    }
}
