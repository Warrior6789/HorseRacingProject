namespace HorseRacingAPI.Models;

public partial class TakeoutConfig
{
    public Guid TakeoutConfigId { get; set; }
    public float TakeoutPercentage { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
