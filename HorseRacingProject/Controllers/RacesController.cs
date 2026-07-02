using HorseRacingAPI.Dtos;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RacesController : ControllerBase
    {
        private readonly IRaceService _raceService;
        private readonly IHubContext<RaceHub> _hubContext;
        private readonly RaceEngineService _engineService;
        private readonly IRaceRefereeService _raceRefereeService;

        public RacesController(IRaceService raceService, IHubContext<RaceHub> hubContext, RaceEngineService engineService, IRaceRefereeService raceRefereeService)
        {
            _raceService = raceService;
            _hubContext = hubContext;
            _engineService = engineService;
            _raceRefereeService = raceRefereeService;
        }

      

        [HttpGet("paged")]
        public async Task<IActionResult> GetRaces(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] Guid? racecourseId = null,
            [FromQuery] string? status = null,
            [FromQuery] string? search = null,
            [FromQuery] string? date = null)
        {
            PagedResponse<RaceResponse> result = await _raceService.GetRacesAsync(page, pageSize, racecourseId, status, search, date);
            return Ok(ApiResponse<PagedResponse<RaceResponse>>.SuccessResponse(result, "Get races successfully."));
        }

        [HttpGet("{raceId}")]
        public async Task<IActionResult> GetRaceById(Guid raceId)
        {
            RaceResponse result = await _raceService.GetRaceByIdAsync(raceId);
            return Ok(ApiResponse<RaceResponse>.SuccessResponse(result, "Get race successfully."));
        }

        [HttpGet("upcoming/paged")]
        public async Task<IActionResult> GetUpcomingRaces(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            List<string>? statuses = status?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            PagedResponse<UpcomingRaceResponse> result = await _raceService.GetUpcomingRacesAsync(page, pageSize, statuses);
            return Ok(ApiResponse<PagedResponse<UpcomingRaceResponse>>.SuccessResponse(result, "Get upcoming races successfully."));
        }

        [HttpGet("{raceId}/registrations")]
        public async Task<IActionResult> GetRaceRegistrations(Guid raceId)
        {
            List<RegistrationResponse> result = await _raceService.GetRaceRegistrationsAsync(raceId);
            return Ok(ApiResponse<List<RegistrationResponse>>.SuccessResponse(result, "Get race registrations successfully."));
        }

        [HttpGet("{raceId}/horses")]
        public async Task<IActionResult> GetRaceHorses(Guid raceId)
        {
            List<RaceResultHorseDto> result = await _raceService.GetRaceHorsesAsync(raceId);
            return Ok(ApiResponse<List<RaceResultHorseDto>>.SuccessResponse(result, "Get race horses successfully."));
        }

        [HttpGet("{raceId}/results")]
        public async Task<IActionResult> GetRaceResults(Guid raceId)
        {
            List<RaceResultResponse> result = await _raceService.GetRaceResultsAsync(raceId);
            return Ok(ApiResponse<List<RaceResultResponse>>.SuccessResponse(result, "Get race results successfully."));
        }

        

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateRace([FromForm] CreateRaceRequest request)
        {
            RaceResponse result = await _raceService.CreateRaceAsync(request);
            return Ok(ApiResponse<RaceResponse>.SuccessResponse(result, "Race created successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{raceId}")]
        public async Task<IActionResult> UpdateRace(Guid raceId, [FromBody] UpdateRaceRequest request)
        {
            RaceResponse result = await _raceService.UpdateRaceAsync(raceId, request);
            return Ok(ApiResponse<RaceResponse>.SuccessResponse(result, "Race updated successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{raceId}/engine/override-result")]
        public IActionResult OverrideResult(Guid raceId, [FromBody] List<HorseRankOverrideDto> ranks)
        {
            var horseRanks = ranks.ToDictionary(r => r.HorseId, r => r.Rank);
            _engineService.OverrideResult(raceId, horseRanks);
            return Ok(ApiResponse<object>.SuccessResponse(null!, "Result override applied."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{raceId}/reset")]
        public async Task<IActionResult> ResetRace(Guid raceId)
        {
            await _raceService.ResetRaceAsync(raceId);
            return Ok(ApiResponse<object>.SuccessResponse(null!, "Race reset to Scheduled successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{raceId}/advance")]
        public async Task<IActionResult> AdvanceRaceStatus(Guid raceId)
        {
            RaceResponse result = await _raceService.AdvanceRaceStatusAsync(raceId);
            return Ok(ApiResponse<RaceResponse>.SuccessResponse(result, $"Race advanced to '{result.Status}' successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{raceId}/pool/collect")]
        public async Task<IActionResult> CollectFromSpectators(Guid raceId, [FromBody] CollectToRacePoolRequest request)
        {
            CollectToRacePoolResponse result = await _raceService.CollectFromSpectatorsAsync(raceId, request);
            return Ok(ApiResponse<CollectToRacePoolResponse>.SuccessResponse(result, $"Collected from {result.ChargedCount} spectators. Total: {result.TotalCollected} coins."));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("{raceId}/pool")]
        public async Task<IActionResult> GetRacePoolOverview(Guid raceId)
        {
            RacePoolOverviewResponse result = await _raceService.GetRacePoolOverviewAsync(raceId);
            return Ok(ApiResponse<RacePoolOverviewResponse>.SuccessResponse(result, "Get race pool overview successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("{raceId}/prize-preview")]
        public async Task<IActionResult> GetPrizePreview(Guid raceId)
        {
            RacePrizePreviewResponse result = await _raceService.GetPrizePreviewAsync(raceId);
            return Ok(ApiResponse<RacePrizePreviewResponse>.SuccessResponse(result, "Get prize preview successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("takeout-ledger/paged")]
        public async Task<IActionResult> GetTakeoutLedgerPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] Guid? raceId = null,
            [FromQuery] string? betType = null)
        {
            PagedResponse<TakeoutLedgerResponse> result = await _raceService.GetTakeoutLedgerPagedAsync(page, pageSize, raceId, betType);
            return Ok(ApiResponse<PagedResponse<TakeoutLedgerResponse>>.SuccessResponse(result, "Get takeout ledger successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{raceId}")]
        public async Task<IActionResult> DeleteRace(Guid raceId)
        {
            await _raceService.DeleteRaceAsync(raceId);
            return Ok(ApiResponse<object>.SuccessResponse("Race deleted successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{raceId}/image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage(Guid raceId, IFormFile file)
        {
            string imageUrl = await _raceService.UploadImageAsync(raceId, file);
            return Ok(ApiResponse<string>.SuccessResponse(imageUrl, "Race image uploaded successfully."));
        }


        [Authorize(Roles = "Referee")]
        [HttpGet("referee/my")]
        public async Task<IActionResult> GetMyAssignedRaces()
        {
            Guid refereeId = GetAccountIdFromToken();
            List<RaceResponse> result = await _raceRefereeService.GetMyAssignedRacesAsync(refereeId);
            return Ok(ApiResponse<List<RaceResponse>>.SuccessResponse(result, "Get assigned races successfully."));
        }

        [Authorize(Roles = "HorseOwner")]
        [HttpPost("{raceId}/register")]
        public async Task<IActionResult> RegisterHorse(Guid raceId, [FromBody] RegisterHorseToRaceRequest request)
        {
            Guid ownerId = GetAccountIdFromToken();
            RegistrationResponse result = await _raceService.RegisterHorseAsync(raceId, ownerId, request);
            return Ok(ApiResponse<RegistrationResponse>.SuccessResponse(result, "Horse registered successfully."));
        }


        private Guid GetAccountIdFromToken()
        {
            string? value = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(value, out Guid accountId))
                throw new UnauthorizedAccessException("Invalid token.");
            return accountId;
        }
    }
}
