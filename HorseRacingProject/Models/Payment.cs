using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class Payment
{
    public string PaymentId { get; set; } = null!;

    public string AccountId { get; set; } = null!;

    public float ConversionRateId { get; set; }

    public decimal Amount { get; set; }

    public string? Status { get; set; }

    public DateTime? CreateAt { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual ConversionRate ConversionRate { get; set; } = null!;
}
