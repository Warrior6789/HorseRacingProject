using System;
using System.Collections.Generic;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class ConversionRate
{
    public Guid ConversionRateId { get; set; }

    public float RateValue { get; set; }

    public ConfigStatus Status { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
