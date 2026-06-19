using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class CreateGradePurseConfigRequest
    {
        [Range(0, 1)] public float G1Ratio { get; set; }
        [Range(0, 1)] public float G2Ratio { get; set; }
        [Range(0, 1)] public float G3Ratio { get; set; }
        [Range(0, 1)] public float ListedRatio { get; set; }
        [Range(0, 1)] public float OpenRatio { get; set; }
    }
}
