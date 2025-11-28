using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ARS.Services;
using Microsoft.Extensions.Logging;

namespace ARS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
    public class SeatController : ControllerBase
    {
        private readonly ISeatService _seatService;
        private readonly ILogger<SeatController> _logger;

        public SeatController(ISeatService seatService, ILogger<SeatController> logger)
        {
            _seatService = seatService;
            _logger = logger;
        }

        // GET: api/seat/map/1000
        [HttpGet("map/{scheduleId}")]
        public async Task<IActionResult> GetSeatMap(int scheduleId)
        {
            var seats = await _seatService.GetAvailableSeatsAsync(scheduleId);
            return Ok(seats);
        }

        public class ReserveRequest
        {
            public int FlightSeatId { get; set; }
            public int ReservationId { get; set; }
        }

        // POST: api/seat/reserve
        [HttpPost("reserve")]
        public async Task<IActionResult> Reserve([FromBody] ReserveRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var ok = await _seatService.ReserveSeatAsync(req.FlightSeatId, req.ReservationId);
            if (ok) return Ok(new { reserved = true });
            return Conflict(new { reserved = false, message = "Seat is no longer available" });
        }

        public class ReserveForLegRequest
        {
            public int FlightSeatId { get; set; }
            public int ReservationLegId { get; set; }
        }

        // POST: api/seat/reserve/leg
        [HttpPost("reserve/leg")]
        public async Task<IActionResult> ReserveForLeg([FromBody] ReserveForLegRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var ok = await _seatService.ReserveSeatForLegAsync(req.FlightSeatId, req.ReservationLegId);
            if (ok) return Ok(new { reserved = true });
            return Conflict(new { reserved = false, message = "Seat is no longer available or leg not found" });
        }

        public class CancelRequest
        {
            public int ReservationId { get; set; }
        }

        // POST: api/seat/cancel
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel([FromBody] CancelRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var ok = await _seatService.CancelReservationSeatAsync(req.ReservationId);
            if (ok) return Ok(new { cancelled = true });
            return NotFound(new { cancelled = false, message = "No flight seat found for reservation" });
        }

        public class CancelLegRequest
        {
            public int ReservationLegId { get; set; }
        }

        // POST: api/seat/cancel/leg
        [HttpPost("cancel/leg")]
        public async Task<IActionResult> CancelForLeg([FromBody] CancelLegRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var ok = await _seatService.CancelReservationSeatForLegAsync(req.ReservationLegId);
            if (ok) return Ok(new { cancelled = true });
            return NotFound(new { cancelled = false, message = "No flight seat found for reservation leg" });
        }
    }
}
