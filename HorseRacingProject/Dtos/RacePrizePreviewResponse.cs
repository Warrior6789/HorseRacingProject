namespace HorseRacingAPI.Dtos
{
    public class RacePrizePreviewItemResponse
    {
        public Guid RegistrationId { get; set; }
        public Guid HorseId { get; set; }
        public string? HorseName { get; set; }
        public Guid OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public Guid JockeyId { get; set; }
        public string? JockeyName { get; set; }
        public int? Position { get; set; }
        public decimal PositionPrize { get; set; }
        public decimal OwnerAmount { get; set; }
        public decimal JockeyAmount { get; set; }
    }

    public class RacePrizePreviewResponse
    {
        public Guid RaceId { get; set; }
        public decimal RacePurse { get; set; }
        public bool IsFinal { get; set; }
        public List<RacePrizePreviewItemResponse> Items { get; set; } = new();
    }
}
