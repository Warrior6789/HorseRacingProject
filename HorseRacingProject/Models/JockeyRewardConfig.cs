using System;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class JockeyRewardConfig
{
    public Guid JockeyRewardConfigId { get; set; }

    public float WinCut { get; set; }

    public float PlaceCut { get; set; }

    public ConfigStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual ICollection<Race> Races { get; set; } = new List<Race>();
}
