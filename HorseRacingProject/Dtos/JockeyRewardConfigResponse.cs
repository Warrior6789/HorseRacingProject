namespace HorseRacingAPI.Dtos
{
    public class JockeyRewardConfigResponse
    {
        public Guid Id { get; set; }
        public float WinCut { get; set; }
        public float PlaceCut { get; set; }
        public string Status { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
