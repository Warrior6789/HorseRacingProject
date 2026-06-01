using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Registration
{
    public string RegistrationId { get; set; } = null!;

    public string RaceId { get; set; } = null!;

    public string HorseId { get; set; } = null!;

    public string JockeyId { get; set; } = null!;

    public int? GateNumber { get; set; }

    public bool? OwnerConfirmation { get; set; }

    public bool? JockeyConfirmation { get; set; }

    public string? Status { get; set; }

    public DateTime? CreateAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Bet> Bets { get; set; } = new List<Bet>();

    public virtual Horse Horse { get; set; } = null!;

    public virtual Account Jockey { get; set; } = null!;

    public virtual ICollection<Prize> Prizes { get; set; } = new List<Prize>();

    public virtual Race Race { get; set; } = null!;

    public virtual ICollection<RaceResult> RaceResults { get; set; } = new List<RaceResult>();
}
