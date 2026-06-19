using System;
using System.Collections.Generic;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class RefereeReport
{
    public Guid ReportId { get; set; }

    public Guid RaceId { get; set; }

    public Guid RefereeId { get; set; }

    public Guid RegistrationId { get; set; }

    public string? IncidentDescription { get; set; }

    public string? PenaltyApplied { get; set; }

    public RefereeReportStatus Status { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public virtual Race Race { get; set; } = null!;

    public virtual Account Referee { get; set; } = null!;

    public virtual Registration Registration { get; set; } = null!;
}
