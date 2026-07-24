namespace HorseRacingAPI.Dtos
{
    public class RegistrationResponse
    {
        public Guid RegistrationId { get; set; }

        public Guid JockeyId { get; set; }
        public string? JockeyName { get; set; }
        public JockeyProfileResponse? Jockey { get; set; }
        public UserProfileResponse? Owner { get; set; }

        public int? GateNumber { get; set; }
        public bool? OwnerConfirmation { get; set; }
        public bool? JockeyConfirmation { get; set; }
        public string? Status { get; set; }

        public HorseResponse Horse { get; set; } = null!;
        public RaceResponse Race { get; set; } = null!;

        public DateTimeOffset? CreateAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
