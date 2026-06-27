using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Helpers
{
    internal static class HorseStatusPolicy
    {
        public static readonly HorseStatus[] ActiveStatuses = [HorseStatus.Healthy, HorseStatus.Resting];

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

        public static bool CanRegisterForRace(HorseStatus status) =>
            status == HorseStatus.Healthy;
    }
}
