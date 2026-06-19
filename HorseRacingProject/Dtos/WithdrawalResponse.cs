namespace HorseRacingAPI.Dtos;

public class WithdrawalResponse
{
    public Guid WithdrawalId { get; set; }
    public Guid AccountId { get; set; }
    public long Amount { get; set; }
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public DateTimeOffset CreateAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
