using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class CreateRaceRequest
    {
        [Required]
        public Guid RacecourseId { get; set; }

        [Required]
        public int RaceNumber { get; set; }

        [MaxLength(100)]
        public string? RaceName { get; set; }

        [Required]
        public DateTimeOffset StartTime { get; set; }

        public float? TrackLength { get; set; }

        [Range(3, int.MaxValue, ErrorMessage = "MaxParticipants must be at least 3.")]
        public int? MaxParticipants { get; set; }

        public IFormFile? Image { get; set; }
    }
}
