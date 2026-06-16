using System;

namespace HorseRacingAPI.Models;

public partial class BetPayoutConfig
{
    public Guid BetPayoutConfigId { get; set; }

    public float WinRatio { get; set; }

    public float PlaceRatio { get; set; }

    public float ShowRatio { get; set; }

    public string? Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual ICollection<Bet> Bets { get; set; } = new List<Bet>();
}
