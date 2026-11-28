using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using degra_parser_api.Data;
using degra_parser_api.Models;

namespace degra_parser_api.Controllers
{
    [ApiController]
    [Route("api/schedules")]
    public class SchedulesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SchedulesController(AppDbContext db) => _db = db;

        // GET api/schedules - full schedules, including parsed Data
        [HttpGet]
        public async Task<ActionResult<List<Schedule>>> GetAll(CancellationToken ct)
        {
            var schedules = await _db.Schedules
                .AsNoTracking()
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync(ct);

            return Ok(schedules);
        }

        // GET api/schedules/{id} - single full schedule
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Schedule>> GetSchedule(Guid id, CancellationToken ct)
        {
            var schedule = await _db.Schedules
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            return schedule is null ? NotFound() : Ok(schedule);
        }

        // GET api/schedules/lite - only Id/Title/UpdatedAt, the jsonb column is not loaded
        [HttpGet("lite")]
        public async Task<ActionResult<List<ScheduleLiteDto>>> GetLite(CancellationToken ct)
        {
            var schedules = await _db.Schedules
                .AsNoTracking()
                .OrderByDescending(s => s.UpdatedAt)
                .Select(s => new ScheduleLiteDto(s.Id, s.Title, s.UpdatedAt))
                .ToListAsync(ct);

            return Ok(schedules);
        }
    }

    public record ScheduleLiteDto(Guid Id, string Title, DateTimeOffset UpdatedAt);
}
