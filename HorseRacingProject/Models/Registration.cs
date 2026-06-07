using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Registration
{
    public Guid RegistrationId { get; set; }

    public Guid RaceId { get; set; }

    public Guid HorseId { get; set; }

    public Guid JockeyId { get; set; }

    public int? GateNumber { get; set; }

    public bool? OwnerConfirmation { get; set; }

    public bool? JockeyConfirmation { get; set; }

    public string? Status { get; set; }

    public DateTimeOffset? CreateAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public virtual ICollection<Bet> Bets { get; set; } = new List<Bet>();

    public virtual Horse Horse { get; set; } = null!;

    public virtual Account Jockey { get; set; } = null!;

    public virtual ICollection<Prize> Prizes { get; set; } = new List<Prize>();

    public virtual Race Race { get; set; } = null!;

    public virtual ICollection<RaceResult> RaceResults { get; set; } = new List<RaceResult>();
}
