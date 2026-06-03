using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class UserProfile
{
    public Guid ProfileId { get; set; }

    public Guid AccountId { get; set; }

    public string? FullName { get; set; }

    public long? Balance { get; set; }

    public string? Phone { get; set; }

    public DateTime? CreateAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
