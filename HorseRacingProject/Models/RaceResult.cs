using System;
using System.Collections.Generic;

namespace HorseRacingAPI.Models;

public partial class RaceResult
{
    public Guid ResultId { get; set; }

    public Guid RegistrationId { get; set; }

    public int? FinishPosition { get; set; }

    public int? FinishTime { get; set; }

    public bool? IsDisqualified { get; set; }

    public DateTimeOffset? CreateAt { get; set; }

    public virtual Registration Registration { get; set; } = null!;
}
