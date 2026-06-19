using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Dtos
{
    public class ConversionRateResponse
    {
        public Guid Id { get; set; }
        public float RateValue { get; set; }
        public ConfigStatus Status { get; set; }
    }
}
