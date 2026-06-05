using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class RefereeReport
{
    public Guid ReportId { get; set; }

    public Guid RaceId { get; set; }

    public Guid RefereeId { get; set; }

    public string? IncidentDescription { get; set; }

    public string? PenaltyApplied { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Race Race { get; set; } = null!;

    public virtual Account Referee { get; set; } = null!;
}
