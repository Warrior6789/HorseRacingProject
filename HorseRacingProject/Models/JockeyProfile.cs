using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class JockeyProfile
{
    public Guid JockeyProfileId { get; set; }

    public Guid AccountId { get; set; }

    public int? ExperienceYears { get; set; }

    public decimal? JockeyRating { get; set; }

    public DateTime? CreateAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
