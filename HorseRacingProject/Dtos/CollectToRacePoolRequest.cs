using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Dtos
{
    public class CollectToRacePoolRequest
    {
        public long AmountPerSpectator { get; set; }
        public BetType BetType { get; set; }
    }
}
