using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Services
{
    internal static class HorseStatusPolicy
    {
        public static readonly string[] ActiveStatuses =
        {
            HorseStatus.Healthy.ToString(),
            HorseStatus.Resting.ToString()
        };

        public static string AllowedStatusMessage =>
            $"Horse status must be one of: {string.Join(", ", Enum.GetNames<HorseStatus>())}.";

        public static HorseStatus NormalizeOrDefault(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return HorseStatus.Healthy;

            return NormalizeRequired(status);
        }

        public static HorseStatus NormalizeRequired(string status)
        {
            string trimmedStatus = status.Trim();

            if (int.TryParse(trimmedStatus, out _))
                throw new InvalidOperationException(AllowedStatusMessage);

            if (!Enum.TryParse(trimmedStatus, ignoreCase: true, out HorseStatus parsedStatus) ||
                !Enum.IsDefined(parsedStatus))
            {
                throw new InvalidOperationException(AllowedStatusMessage);
            }

            return parsedStatus;
        }

        public static bool CanRegisterForRace(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            if (!Enum.TryParse(status.Trim(), ignoreCase: true, out HorseStatus parsedStatus) ||
                !Enum.IsDefined(parsedStatus))
            {
                return false;
            }

            return parsedStatus == HorseStatus.Healthy || parsedStatus == HorseStatus.Resting;
        }
    }
}
