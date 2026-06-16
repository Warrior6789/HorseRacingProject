using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Dtos
{
    public class BetPayoutConfigResponse
    {
        public Guid Id { get; set; }
        public float WinRatio { get; set; }
        public float PlaceRatio { get; set; }
        public float ShowRatio { get; set; }
        public BetPayoutConfigStatus Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
