using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class JockeyProfile
{
    public Guid JockeyProfileId { get; set; }

    public Guid AccountId { get; set; }

    public int? ExperienceYears { get; set; }

    public decimal? JockeyRating { get; set; }

    public DateTimeOffset? CreateAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
