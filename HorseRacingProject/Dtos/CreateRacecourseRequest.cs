using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class CreateRacecourseRequest
    {
        [Required]
        [MaxLength(150)]
        public string RacecourseName { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string? Location { get; set; }

        [Required]
        [MaxLength(50)]
        public string? TrackType { get; set; }

        public IFormFile? Image { get; set; }
    }
}
