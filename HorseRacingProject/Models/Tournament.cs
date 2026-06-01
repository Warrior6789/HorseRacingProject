using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Tournament
{
    public string Id { get; set; } = null!;

    public string TournamentName { get; set; } = null!;

    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Status { get; set; }

    public DateTime? CreateAt { get; set; }

    public virtual ICollection<Race> Races { get; set; } = new List<Race>();
}
