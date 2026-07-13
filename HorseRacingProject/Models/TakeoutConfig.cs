using System.Collections.Generic;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class TakeoutConfig
{
    public Guid TakeoutConfigId { get; set; }
    public float TakeoutPercentage { get; set; }
    public ConfigStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public virtual ICollection<Race> Races { get; set; } = new List<Race>();
}
