using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class TakeoutLedger
{
    public Guid TakeoutLedgerId { get; set; }
    public Guid RaceId { get; set; }
    public BetType BetType { get; set; }
    public decimal TotalPool { get; set; }
    public float TakeoutPercentage { get; set; }
    public decimal TakeoutAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public virtual Race Race { get; set; } = null!;
}
