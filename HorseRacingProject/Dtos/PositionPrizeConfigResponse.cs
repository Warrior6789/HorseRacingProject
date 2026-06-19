namespace HorseRacingAPI.Dtos
{
    public class PositionPrizeConfigResponse
    {
        public Guid Id { get; set; }
        public float Pos1Ratio { get; set; }
        public float Pos2Ratio { get; set; }
        public float Pos3Ratio { get; set; }
        public float Pos4Ratio { get; set; }
        public float Pos5Ratio { get; set; }
        public float Pos6Ratio { get; set; }
        public string Status { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
