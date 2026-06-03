using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Racecourse
{
    public Guid Id { get; set; }

    public string RacecourseName { get; set; } = null!;

    public string? Location { get; set; }

    public string? TrackType { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Race> Races { get; set; } = new List<Race>();
}
