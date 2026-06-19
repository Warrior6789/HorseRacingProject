using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class CreatePositionPrizeConfigRequest
    {
        [Range(0, 1)] public float Pos1Ratio { get; set; }
        [Range(0, 1)] public float Pos2Ratio { get; set; }
        [Range(0, 1)] public float Pos3Ratio { get; set; }
        [Range(0, 1)] public float Pos4Ratio { get; set; }
        [Range(0, 1)] public float Pos5Ratio { get; set; }
        [Range(0, 1)] public float Pos6Ratio { get; set; }
    }
}
