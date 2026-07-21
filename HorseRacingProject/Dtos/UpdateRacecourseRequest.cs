using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class UpdateRacecourseRequest
    {
        [MaxLength(150)]
        public string? RacecourseName { get; set; }

        [Required]
        [MaxLength(255)]
        public string? Location { get; set; }

        [MaxLength(50)]
        public string? TrackType { get; set; }
    }
}
