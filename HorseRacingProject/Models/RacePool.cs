using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class RacePool
{
    public Guid RacePoolId { get; set; }
    public Guid RaceId { get; set; }
    public BetType BetType { get; set; }
    public decimal TotalAmount { get; set; }
    public virtual Race Race { get; set; } = null!;
}
