using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TournamentsController : ControllerBase
    {
        private readonly ITournamentService _tournamentService;

        public TournamentsController(ITournamentService tournamentService)
        {
            _tournamentService = tournamentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            List<TournamentResponse> tournaments = await _tournamentService.GetAllTournamentsAsync();
            return Ok(ApiResponse<List<TournamentResponse>>.SuccessResponse(tournaments, "Get tournaments successfully."));
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<TournamentResponse> tournaments = await _tournamentService.GetAllTournamentsPagingAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<TournamentResponse>>.SuccessResponse(tournaments, "Get tournaments successfully."));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            TournamentResponse tournament = await _tournamentService.GetTournamentsByIdAsync(id);
            return Ok(ApiResponse<TournamentResponse>.SuccessResponse(tournament, "Get tournament successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTournamentRequest request)
        {
            TournamentResponse tournament = await _tournamentService.CreateTournamentAsync(request);
            return Ok(ApiResponse<TournamentResponse>.SuccessResponse(tournament, "Tournament created successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTournamentRequest request)
        {
            TournamentResponse tournament = await _tournamentService.UpdateTournamentAsync(id, request);
            return Ok(ApiResponse<TournamentResponse>.SuccessResponse(tournament, "Tournament updated successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _tournamentService.DeleteTournamentAsync(id);
            return Ok(ApiResponse<object>.SuccessResponse("Tournament deleted successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromQuery] TournamentStatus status)
        {
            await _tournamentService.ChangeTournamentStatusAsync(id, status);
            return Ok(ApiResponse<object>.SuccessResponse("Tournament status updated successfully."));
        }
    }
}
