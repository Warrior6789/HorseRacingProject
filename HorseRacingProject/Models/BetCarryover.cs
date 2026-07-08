using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class BetCarryover
{
    public Guid BetCarryoverId { get; set; }

    public Guid RacecourseId { get; set; }

    public BetType BetType { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual Racecourse Racecourse { get; set; } = null!;
}
