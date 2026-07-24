namespace HorseRacingAPI.Dtos;

public class PaymentResponse
{
    public Guid PaymentId { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountEmail { get; set; }
    public decimal Amount { get; set; }
    public long BalanceChanged { get; set; }
    public long CurrentBalance { get; set; }
    public string? Status { get; set; }
    public string? TransactionType { get; set; }
    public DateTimeOffset? CreateAt { get; set; }
}
