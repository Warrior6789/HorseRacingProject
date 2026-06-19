using System;
using System.Collections.Generic;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class Tournament
{
    public Guid Id { get; set; }

    public string TournamentName { get; set; } = null!;

    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public TournamentStatus Status { get; set; }

    public decimal FundsPrize { get; set; }

    public DateTimeOffset? CreateAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public virtual ICollection<Race> Races { get; set; } = new List<Race>();
}
