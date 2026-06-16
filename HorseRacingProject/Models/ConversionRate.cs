using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class ConversionRate
{
    public Guid ConversionRateId { get; set; }

    public float RateValue { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
