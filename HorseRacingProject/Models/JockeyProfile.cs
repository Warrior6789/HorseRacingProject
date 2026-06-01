using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class JockeyProfile
{
    public string JockeyProfileId { get; set; } = null!;

    public string AccountId { get; set; } = null!;

    public int? ExperienceYears { get; set; }

    public decimal? JockeyRating { get; set; }

    public DateTime? CreateAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
