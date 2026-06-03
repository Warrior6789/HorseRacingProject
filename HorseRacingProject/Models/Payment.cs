using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Payment
{
    public Guid PaymentId { get; set; }

    public Guid AccountId { get; set; }

    public Guid ConversionRateId { get; set; }

    public decimal Amount { get; set; }

    public string? Status { get; set; }

    public DateTime? CreateAt { get; set; }

    public virtual ConversionRate ConversionRate { get; set; } = null!;
}
