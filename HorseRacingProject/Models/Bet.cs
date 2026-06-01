using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Bet
{
    public string BetId { get; set; } = null!;

    public string SpectatorId { get; set; } = null!;

    public string RegistrationId { get; set; } = null!;

    public decimal BetAmount { get; set; }

    public string? BetType { get; set; }

    public float? PayoutRatio { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Registration Registration { get; set; } = null!;

    public virtual Account Spectator { get; set; } = null!;
}
