using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos;

public class DepositRequest
{
    [Required]
    [Range(10000, 100000000, ErrorMessage = "Amount must be between 10,000 and 100,000,000.")]
    public decimal Amount { get; set; }
}
