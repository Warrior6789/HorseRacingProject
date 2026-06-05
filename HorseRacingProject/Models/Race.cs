using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Race
{
    public Guid RaceId { get; set; }

    public Guid TournamentId { get; set; }

    public Guid RacecourseId { get; set; }

    public int? RaceNumber { get; set; }

    public DateTime? StartTime { get; set; }

    public float? TrackLength { get; set; }

    public int? MaxParticipants { get; set; }

    public string? Status { get; set; }

    public DateTime? CreateAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Racecourse Racecourse { get; set; } = null!;

    public virtual ICollection<RefereeReport> RefereeReports { get; set; } = new List<RefereeReport>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual Tournament Tournament { get; set; } = null!;
}
