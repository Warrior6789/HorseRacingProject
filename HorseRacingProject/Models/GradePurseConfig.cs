using System;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class GradePurseConfig
{
    public Guid GradePurseConfigId { get; set; }

    public float G1Ratio { get; set; }

    public float G2Ratio { get; set; }

    public float G3Ratio { get; set; }

    public float ListedRatio { get; set; }

    public float OpenRatio { get; set; }

    public ConfigStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual ICollection<Race> Races { get; set; } = new List<Race>();
}
