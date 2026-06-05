using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Prize
{
    public Guid PrizeId { get; set; }

    public Guid RegistrationId { get; set; }

    public string? PrizeType { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? DistributedAt { get; set; }

    public virtual Registration Registration { get; set; } = null!;
}
