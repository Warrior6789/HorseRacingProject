using HorseRacingAPI.Dtos;
using HorseRacingAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RacecoursesController : ControllerBase
    {
        private readonly IRacecourseService _racecourseService;

        public RacecoursesController(IRacecourseService racecourseService)
        {
            _racecourseService = racecourseService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            List<RacecourseResponse> racecourses = await _racecourseService.GetAllRacecoursesAsync();
            return Ok(ApiResponse<List<RacecourseResponse>>.SuccessResponse(racecourses, "Get racecourses successfully."));
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            PagedResponse<RacecourseResponse> racecourses = await _racecourseService.GetAllRacecoursesPagingAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<RacecourseResponse>>.SuccessResponse(racecourses, "Get racecourses successfully."));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            RacecourseResponse racecourse = await _racecourseService.GetRacecourseByIdAsync(id);
            return Ok(ApiResponse<RacecourseResponse>.SuccessResponse(racecourse, "Get racecourse successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRacecourseRequest request)
        {
            RacecourseResponse racecourse = await _racecourseService.CreateRacecourseAsync(request);
            return Ok(ApiResponse<RacecourseResponse>.SuccessResponse(racecourse, "Racecourse created successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRacecourseRequest request)
        {
            RacecourseResponse racecourse = await _racecourseService.UpdateRacecourseAsync(id, request);
            return Ok(ApiResponse<RacecourseResponse>.SuccessResponse(racecourse, "Racecourse updated successfully."));
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _racecourseService.DeleteRacecourseAsync(id);
            return Ok(ApiResponse<object>.SuccessResponse("Racecourse deleted successfully."));
        }
    }
}
