using System;

namespace HorseRacingAPI.Models;

public partial class PositionPrizeConfig
{
    public Guid PositionPrizeConfigId { get; set; }

    public float Pos1Ratio { get; set; }

    public float Pos2Ratio { get; set; }

    public float Pos3Ratio { get; set; }

    public float Pos4Ratio { get; set; }

    public float Pos5Ratio { get; set; }

    public float Pos6Ratio { get; set; }

    public string? Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual ICollection<Race> Races { get; set; } = new List<Race>();
}
