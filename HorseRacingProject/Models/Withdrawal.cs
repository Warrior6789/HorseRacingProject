using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class Withdrawal
{
    public Guid WithdrawalId { get; set; }
    public Guid AccountId { get; set; }
    public long Amount { get; set; }
    public string BankAccountNumber { get; set; } = null!;
    public string BankName { get; set; } = null!;
    public string AccountHolderName { get; set; } = null!;
    public WithdrawalStatus Status { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset CreateAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
