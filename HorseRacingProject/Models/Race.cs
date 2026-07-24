using System;
using System.Collections.Generic;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class Race
{
    public Guid RaceId { get; set; }

    public Guid RacecourseId { get; set; }

    public int? RaceNumber { get; set; }

    public string? RaceName { get; set; }

    public DateTimeOffset? StartTime { get; set; }

    public float? TrackLength { get; set; }

    public int? MaxParticipants { get; set; }

    public RaceStatus Status { get; set; }

    public decimal RegistrationFee { get; set; } = 0;

    public decimal PrizePool { get; set; } = 0;

    public string? ImageUrl { get; set; }

    public DateTimeOffset? CreateAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? RegistrationFeeConfigId { get; set; }

    public Guid? PositionPrizeConfigId { get; set; }

    public Guid? JockeyRewardConfigId { get; set; }

    public Guid? TakeoutConfigId { get; set; }

    public Guid? RefereeId { get; set; }

    public virtual Racecourse Racecourse { get; set; } = null!;

    public virtual RegistrationFeeConfig? RegistrationFeeConfig { get; set; }

    public virtual PositionPrizeConfig? PositionPrizeConfig { get; set; }

    public virtual JockeyRewardConfig? JockeyRewardConfig { get; set; }

    public virtual TakeoutConfig? TakeoutConfig { get; set; }

    public virtual Account? Referee { get; set; }

    public virtual ICollection<RefereeReport> RefereeReports { get; set; } = new List<RefereeReport>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual ICollection<RacePool> RacePools { get; set; } = new List<RacePool>();

    public virtual ICollection<TakeoutLedger> TakeoutLedgers { get; set; } = new List<TakeoutLedger>();
}
