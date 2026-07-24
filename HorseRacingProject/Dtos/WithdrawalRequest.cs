using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos;

public class WithdrawalRequest
{
    public long Amount { get; set; }

    [Required(ErrorMessage = "Bank account number is required.")]
    [RegularExpression(@"^\d{6,19}$", ErrorMessage = "Bank account number must be 6-19 digits.")]
    public string BankAccountNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bank name is required.")]
    [MaxLength(100)]
    public string BankName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Account holder name is required.")]
    [MaxLength(100)]
    public string AccountHolderName { get; set; } = string.Empty;
}
