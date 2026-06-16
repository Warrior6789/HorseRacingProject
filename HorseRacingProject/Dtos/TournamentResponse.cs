namespace HorseRacingAPI.Dtos
{
    public class TournamentResponse
    {
        public Guid TournamentId { get; set; }
        public string? TournamentName { get; set; }
        public string? Description { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? Status { get; set; }
        public decimal FundsPrize { get; set; }
    }
}
