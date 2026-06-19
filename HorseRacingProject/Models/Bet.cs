namespace HorseRacingAPI.Models;

public partial class Bet
{
    public Guid BetId { get; set; }

    public Guid SpectatorId { get; set; }

    public Guid RegistrationId { get; set; }

    public decimal BetAmount { get; set; }

    public string? BetType { get; set; }

    public float? PayoutRatio { get; set; }

    public string? Status { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public virtual Registration Registration { get; set; } = null!;

    public virtual Account Spectator { get; set; } = null!;
}
