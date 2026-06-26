using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RefereeReportsController : ControllerBase
    {
        private readonly IRefereeReportService _refereeReportService;

        public RefereeReportsController(IRefereeReportService refereeReportService)
        {
            _refereeReportService = refereeReportService;
        }

        [HttpPost]
        [Authorize(Roles = "Referee")]
        public async Task<IActionResult> Create([FromBody] CreateRefereeReportDto dto)
        {
            Guid refereeId = GetAccountIdFromToken();
            RefereeReportResponse result = await _refereeReportService.CreateReportAsync(refereeId, dto);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<RefereeReportResponse>.SuccessResponse(result, "Referee report created successfully."));
        }

        [HttpGet("{reportId}")]
        [Authorize(Roles = "Admin,Referee")]
        public async Task<IActionResult> GetById(Guid reportId)
        {
            Guid requesterId = GetAccountIdFromToken();
            bool isAdmin = User.IsInRole("Admin");
            RefereeReportResponse result = await _refereeReportService.GetReportByIdAsync(reportId, requesterId, isAdmin);
            return Ok(ApiResponse<RefereeReportResponse>.SuccessResponse(result, "Get report successfully."));
        }

        [HttpPut("{reportId}")]
        [Authorize(Roles = "Referee")]
        public async Task<IActionResult> Update(Guid reportId, [FromBody] UpdateRefereeReportDto dto)
        {
            Guid refereeId = GetAccountIdFromToken();
            RefereeReportResponse result = await _refereeReportService.UpdateReportAsync(reportId, refereeId, dto);
            return Ok(ApiResponse<RefereeReportResponse>.SuccessResponse(result, "Report updated successfully."));
        }

        [HttpGet("my/paged")]
        [Authorize(Roles = "Referee")]
        public async Task<IActionResult> GetMyReports([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            Guid refereeId = GetAccountIdFromToken();
            PagedResponse<RefereeReportResponse> result = await _refereeReportService.GetMyReportsPagedAsync(refereeId, page, pageSize);
            return Ok(ApiResponse<PagedResponse<RefereeReportResponse>>.SuccessResponse(result, "Get my reports successfully."));
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Referee")]
        public async Task<IActionResult> GetReports([FromQuery] Guid? raceId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            bool isAdmin = User.IsInRole("Admin");
            bool isReferee = User.IsInRole("Referee") && !isAdmin;

            if (isReferee && raceId == null)
                return BadRequest(ApiResponse<object>.FailResponse("Referee must provide raceId."));

            Guid? refereeId = isReferee ? GetAccountIdFromToken() : null;
            RefereeReportPagedResponse result = await _refereeReportService.GetReportsByRaceAsync(raceId, page, pageSize, refereeId);
            return Ok(ApiResponse<RefereeReportPagedResponse>.SuccessResponse(result, "Get referee reports successfully."));
        }

        [HttpPut("{id}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(Guid id)
        {
            RefereeReportResponse result = await _refereeReportService.ApproveReportAsync(id);
            return Ok(ApiResponse<RefereeReportResponse>.SuccessResponse(result, "Referee report approved successfully."));
        }

        [HttpPut("{id}/reject")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(Guid id)
        {
            RefereeReportResponse result = await _refereeReportService.RejectReportAsync(id);
            return Ok(ApiResponse<RefereeReportResponse>.SuccessResponse(result, "Referee report rejected successfully."));
        }

        private Guid GetAccountIdFromToken()
        {
            string? value = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(value, out Guid id))
                throw new UnauthorizedAccessException("Invalid token.");
            return id;
        }
    }
}
