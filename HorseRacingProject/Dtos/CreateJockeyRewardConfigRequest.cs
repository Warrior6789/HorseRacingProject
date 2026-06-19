using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class CreateJockeyRewardConfigRequest
    {
        [Range(0, 1)] public float WinCut { get; set; }
        [Range(0, 1)] public float PlaceCut { get; set; }
    }
}
