using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class RefereeReport
{
    public string ReportId { get; set; } = null!;

    public string RaceId { get; set; } = null!;

    public string RefereeId { get; set; } = null!;

    public string? IncidentDescription { get; set; }

    public string? PenaltyApplied { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Race Race { get; set; } = null!;

    public virtual Account Referee { get; set; } = null!;
}
