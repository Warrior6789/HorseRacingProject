using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class CreateRegistrationFeeConfigRequest
    {
        [Required]
        [Range(0, double.MaxValue)]
        public decimal FeeAmount { get; set; }
    }
}
