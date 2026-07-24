using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/races/{raceId}/referee")]
    [ApiController]
    public class RaceRefereesController : ControllerBase
    {
        private readonly IRaceRefereeService _service;

        public RaceRefereesController(IRaceRefereeService service)
        {
            _service = service;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Get(Guid raceId)
        {
            RaceRefereeResponse? result = await _service.GetByRaceAsync(raceId);
            return Ok(ApiResponse<RaceRefereeResponse?>.SuccessResponse(result, "Get race referee successfully."));
        }

        [HttpPut]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Assign(Guid raceId, [FromBody] AssignRefereeRequest request)
        {
            RaceRefereeResponse result = await _service.AssignAsync(raceId, request.RefereeId);
            return Ok(ApiResponse<RaceRefereeResponse>.SuccessResponse(result, "Referee assigned successfully."));
        }

        [HttpDelete]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Unassign(Guid raceId)
        {
            await _service.UnassignAsync(raceId);
            return Ok(ApiResponse<object>.SuccessResponse(null!, "Referee unassigned successfully."));
        }
    }
}
