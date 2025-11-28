using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using ARS.Data;
using ARS.Models;

namespace ARS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Schedule/Create?flightId=123
        public async Task<IActionResult> Create(int flightId)
        {
            var flight = await _context.Flights.FindAsync(flightId);
            if (flight == null) return NotFound();

            ViewData["Flight"] = flight;
            await PopulateCityDropdowns();
            return View(new Schedule { FlightID = flightId, Date = DateOnly.FromDateTime(DateTime.Now) });
        }

        // POST: Schedule/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FlightID,Date,CityID")] Schedule schedule)
        {
            if (!ModelState.IsValid)
            {
                var flight = await _context.Flights.FindAsync(schedule.FlightID);
                ViewData["Flight"] = flight;
                await PopulateCityDropdowns();
                return View(schedule);
            }

            // prevent duplicate schedule for same flight/date
            var exists = await _context.Schedules.AnyAsync(s => s.FlightID == schedule.FlightID && s.Date == schedule.Date);
            if (exists)
            {
                ModelState.AddModelError(string.Empty, "A schedule for this flight on the selected date already exists.");
                var flight = await _context.Flights.FindAsync(schedule.FlightID);
                ViewData["Flight"] = flight;
                await PopulateCityDropdowns();
                return View(schedule);
            }

            _context.Schedules.Add(schedule);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Flight", new { OriginCityID = 0, DestinationCityID = 0, TravelDate = schedule.Date });
        }

        private async Task PopulateCityDropdowns()
        {
            var cities = await _context.Cities.OrderBy(c => c.CityName).ToListAsync();
            ViewData["Cities"] = new SelectList(cities, "CityID", "CityName");
            ViewData["OriginCityID"] = new SelectList(cities, "CityID", "CityName");
            ViewData["DestinationCityID"] = new SelectList(cities, "CityID", "CityName");
        }
    }
}
