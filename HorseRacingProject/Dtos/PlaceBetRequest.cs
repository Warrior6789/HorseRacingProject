namespace HorseRacingAPI.Dtos
{
    public class PlaceBetRequest
    {
        public Guid RegistrationId { get; set; }
        public string BetType { get; set; } = string.Empty; 
        public decimal BetAmount { get; set; }
    }
}
