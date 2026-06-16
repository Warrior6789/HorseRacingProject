namespace HorseRacingAPI.Dtos
{
    public class CreateBetPayoutConfigRequest
    {
        public float WinRatio { get; set; }
        public float PlaceRatio { get; set; }
        public float ShowRatio { get; set; }
    }
}
