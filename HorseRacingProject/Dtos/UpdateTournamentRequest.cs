using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class UpdateTournamentRequest
    {
        [MaxLength(200)]
        public string? TournamentName { get; set; }

        public string? Description { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        public decimal? FundsPrize { get; set; }
    }
}
