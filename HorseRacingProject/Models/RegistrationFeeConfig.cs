using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Models;

public partial class RegistrationFeeConfig
{
    public Guid RegistrationFeeConfigId { get; set; }
    public decimal FeeAmount { get; set; }
    public ConfigStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public virtual ICollection<Race> Races { get; set; } = new List<Race>();
}
