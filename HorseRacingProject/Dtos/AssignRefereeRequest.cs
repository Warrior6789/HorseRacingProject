using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class AssignRefereeRequest
    {
        [Required]
        public Guid RefereeId { get; set; }
    }
}
