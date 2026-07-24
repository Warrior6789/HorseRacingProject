namespace HorseRacingAPI.Dtos;

public class WalletTransactionResponse
{
    public Guid WalletTransactionId { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountEmail { get; set; }
    public string Type { get; set; } = string.Empty;
    public long Amount { get; set; }
    public long BalanceAfter { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
