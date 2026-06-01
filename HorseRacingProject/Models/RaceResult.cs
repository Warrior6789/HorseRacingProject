using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class RaceResult
{
    public string ResultId { get; set; } = null!;

    public string RegistrationId { get; set; } = null!;

    public int? FinishPosition { get; set; }

    public int? FinishTime { get; set; }

    public bool? IsDisqualified { get; set; }

    public DateTime? CreateAt { get; set; }

    public virtual Registration Registration { get; set; } = null!;
}
