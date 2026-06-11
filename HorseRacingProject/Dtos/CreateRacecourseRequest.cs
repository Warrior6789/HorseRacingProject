using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class CreateRacecourseRequest
    {
        [Required]
        [MaxLength(150)]
        public string RacecourseName { get; set; } = null!;

        [MaxLength(255)]
        public string? Location { get; set; }

        [MaxLength(50)]
        public string? TrackType { get; set; }
    }
}
