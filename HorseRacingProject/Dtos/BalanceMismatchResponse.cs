namespace HorseRacingAPI.Dtos;

public class BalanceMismatchResponse
{
    public Guid AccountId { get; set; }
    public string ProfileType { get; set; } = string.Empty;
    public long CurrentBalance { get; set; }
    public long LedgerBalance { get; set; }
    public long Difference { get; set; }
}
