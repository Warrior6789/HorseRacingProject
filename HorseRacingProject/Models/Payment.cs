using System;
using System.Collections.Generic;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class Payment
{
    public Guid PaymentId { get; set; }

    public Guid AccountId { get; set; }

    public decimal Amount { get; set; }

    public long? OrderCode { get; set; }

    public PaymentStatus Status { get; set; }

    public DateTimeOffset? CreateAt { get; set; }

    public long? BalanceChanged { get; set; }

    public long? CurrentBalance { get; set; }
}
