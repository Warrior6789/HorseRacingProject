namespace HorseRacingAPI.Services
{
    public interface IRaceSettlementService
    {
        Task TrySettleAsync(Guid raceId);
    }
}
