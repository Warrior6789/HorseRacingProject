using System;
using System.Collections.Generic;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class Account
{
    public Guid Id { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string Email { get; set; } = null!;

    public AccountRole Role { get; set; } = AccountRole.Spectator;

    public AccountStatus Status { get; set; }

    public DateTimeOffset? CreateAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public virtual ICollection<Bet> Bets { get; set; } = new List<Bet>();

    public virtual ICollection<Horse> Horses { get; set; } = new List<Horse>();

    public virtual ICollection<JockeyProfile> JockeyProfiles { get; set; } = new List<JockeyProfile>();

    public virtual ICollection<RefereeReport> RefereeReports { get; set; } = new List<RefereeReport>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual ICollection<UserProfile> UserProfiles { get; set; } = new List<UserProfile>();
}
