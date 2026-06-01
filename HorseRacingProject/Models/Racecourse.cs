using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Racecourse
{
    public string Id { get; set; } = null!;

    public string RacecourseName { get; set; } = null!;

    public string? Location { get; set; }

    public string? TrackType { get; set; }

    public virtual ICollection<Race> Races { get; set; } = new List<Race>();
}
