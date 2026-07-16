using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class WalletTransaction
{
    public Guid WalletTransactionId { get; set; }
    public Guid AccountId { get; set; }
    public WalletTransactionType Type { get; set; }
    public long Amount { get; set; }
    public long BalanceAfter { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
