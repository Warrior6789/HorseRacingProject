namespace HorseRacingAPI.Dtos
{
    public class HorseRewardItemResponse
    {
        public Guid PrizeId { get; set; }
        public Guid RegistrationId { get; set; }
        public Guid RaceId { get; set; }
        public int? RaceNumber { get; set; }
        public Guid TournamentId { get; set; }
        public string? TournamentName { get; set; }
        public string? PrizeType { get; set; }
        public decimal? Amount { get; set; }
        public DateTimeOffset? DistributedAt { get; set; }
    }
}
