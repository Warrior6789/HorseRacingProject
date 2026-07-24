namespace HorseRacingAPI.Dtos
{
    public class HorseScheduleResponse
    {
        public Guid HorseId { get; set; }
        public string HorseName { get; set; } = string.Empty;
        public Guid RegistrationId { get; set; }
        public Guid JockeyId { get; set; }
        public int? GateNumber { get; set; }
        public bool? OwnerConfirmation { get; set; }
        public bool? JockeyConfirmation { get; set; }
        public string? RegistrationStatus { get; set; }
        public Guid RaceId { get; set; }
        public int? RaceNumber { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public float? TrackLength { get; set; }
        public int? MaxParticipants { get; set; }
        public string? RaceStatus { get; set; }
        public string? RacecourseName { get; set; }
        public string? Location { get; set; }
    }
}
