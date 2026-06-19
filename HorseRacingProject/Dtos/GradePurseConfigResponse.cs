namespace HorseRacingAPI.Dtos
{
    public class GradePurseConfigResponse
    {
        public Guid Id { get; set; }
        public float G1Ratio { get; set; }
        public float G2Ratio { get; set; }
        public float G3Ratio { get; set; }
        public float ListedRatio { get; set; }
        public float OpenRatio { get; set; }
        public string Status { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
