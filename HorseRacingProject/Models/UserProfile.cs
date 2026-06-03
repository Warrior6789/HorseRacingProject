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

    public DateTimeOffset? CreateAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
