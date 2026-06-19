namespace HorseRacingAPI.Dtos;

public class WithdrawalRequest
{
    public long Amount { get; set; }
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
}
