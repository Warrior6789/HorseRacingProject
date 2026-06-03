using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Horse
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string HorseName { get; set; } = null!;

    public int? Age { get; set; }

    public string? Breed { get; set; }

    public float? Weight { get; set; }

    public string? Status { get; set; }

    public int? RecordWins { get; set; }

    public string? Color { get; set; }

    public DateTime? CreateAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Account Owner { get; set; } = null!;

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
