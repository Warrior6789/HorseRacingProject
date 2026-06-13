using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos;

public class WithdrawRequest
{
    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "Balance amount must be greater than 0.")]
    public long BalanceAmount { get; set; }
}
