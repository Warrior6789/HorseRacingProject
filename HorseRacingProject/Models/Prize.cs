using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Prize
{
    public string PrizeId { get; set; } = null!;

    public string RegistrationId { get; set; } = null!;

    public string? PrizeType { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? DistributedAt { get; set; }

    public virtual Registration Registration { get; set; } = null!;
}
