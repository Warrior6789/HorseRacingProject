namespace HorseRacingAPI.Dtos
{
    public class HorseQueryRequest
    {
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Breed { get; set; }
        public string? Color { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
