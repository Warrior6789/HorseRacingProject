using System;
using System.Collections.Generic;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class Prize
{
    public Guid PrizeId { get; set; }

    public Guid RegistrationId { get; set; }

    public PrizeType? PrizeType { get; set; }

    public decimal? Amount { get; set; }

    public DateTimeOffset? DistributedAt { get; set; }

    public virtual Registration Registration { get; set; } = null!;
}
