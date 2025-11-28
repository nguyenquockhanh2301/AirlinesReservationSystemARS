using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ARS.Data;
using ARS.Models;
using ARS.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace ARS.Controllers
{
    public class FlightController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ARS.Services.ISeatService _seatService;

        public FlightController(ApplicationDbContext context, ARS.Services.ISeatService seatService)
        {
            _context = context;
            _seatService = seatService;
        }

        // GET: Flight
        // Consolidated flights page at /Flight (supports filter query parameters)
        public async Task<IActionResult> Index([FromQuery] FlightSearchViewModel? search)
        {
            await PopulateCityDropdowns();

            // Provide defaults when values aren't supplied
            if (search == null)
            {
                search = new FlightSearchViewModel();
            }

            if (string.IsNullOrEmpty(search.TripType))
                search.TripType = "OneWay";

            if (search.Passengers < 1)
                search.Passengers = 1;

            if (string.IsNullOrEmpty(search.Class))
                search.Class = "Economy";

            if (search.TravelDate == default)
                search.TravelDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1));

            if (search.Page < 1)
                search.Page = 1;

            // Load flights with related data
            var flightsQuery = _context.Flights
                .Include(f => f.OriginCity)
                .Include(f => f.DestinationCity)
                .Include(f => f.Schedules)
                .Include(f => f.Reservations)
                .AsQueryable();

            // Apply origin/destination filters if provided (only filter if not null and not 0)
            if (search.OriginCityID.HasValue && search.OriginCityID.Value != 0)
                flightsQuery = flightsQuery.Where(f => f.OriginCityID == search.OriginCityID.Value);
            if (search.DestinationCityID.HasValue && search.DestinationCityID.Value != 0)
                flightsQuery = flightsQuery.Where(f => f.DestinationCityID == search.DestinationCityID.Value);

            // If a travel date is provided, only include flights that have a schedule on that date
            if (search.TravelDate != default)
                flightsQuery = flightsQuery.Where(f => f.Schedules.Any(s => s.Date == search.TravelDate));

            var flights = await flightsQuery.ToListAsync();

            var flightResults = new List<FlightResultItem>();
            foreach (var f in flights)
            {
                // Prefer the first schedule on or after the requested travel date, otherwise the earliest
                var schedule = f.Schedules
                    .Where(s => s.Date >= search.TravelDate)
                    .OrderBy(s => s.Date)
                    .FirstOrDefault()
                    ?? f.Schedules.OrderBy(s => s.Date).FirstOrDefault();

                // Use schedule date to compute departure/arrival datetimes when available
                var travelDateForCalc = schedule?.Date ?? search.TravelDate;
                var departure = schedule != null
                    ? schedule.Date.ToDateTime(TimeOnly.FromDateTime(f.DepartureTime))
                    : f.DepartureTime;
                var arrival = schedule != null
                    ? schedule.Date.ToDateTime(TimeOnly.FromDateTime(f.ArrivalTime))
                    : f.ArrivalTime;

                // Calculate available seats from FlightSeats table (per-schedule inventory)
                var availableSeats = f.TotalSeats; // default if no schedule
                if (schedule != null)
                {
                    // Ensure FlightSeats exist for this schedule
                    await _seatService.GenerateFlightSeatsAsync(schedule.ScheduleID);
                    
                    // Count available FlightSeats
                    var reservedCount = await _context.FlightSeats
                        .Where(fs => fs.ScheduleId == schedule.ScheduleID && fs.Status == FlightSeatStatus.Reserved)
                        .CountAsync();
                    availableSeats = f.TotalSeats - reservedCount;
                }

                // Skip flights that don't have enough seats
                if (availableSeats < search.Passengers) continue;

                // Price multipliers
                var basePrice = f.BaseFare;
                var classMultiplier = search.Class switch
                {
                    "Business" => 2.0m,
                    "First" => 3.5m,
                    _ => 1.0m
                };

                var daysBeforeDeparture = (travelDateForCalc.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
                var timingMultiplier = daysBeforeDeparture switch
                {
                    >= 30 => 0.80m,
                    >= 15 => 1.00m,
                    >= 7 => 1.20m,
                    _ => 1.50m
                };

                flightResults.Add(new FlightResultItem
                {
                    FlightID = f.FlightID,
                    FlightNumber = f.FlightNumber,
                    OriginCity = f.OriginCity?.CityName ?? "",
                    OriginAirportCode = f.OriginCity?.AirportCode ?? "",
                    DestinationCity = f.DestinationCity?.CityName ?? "",
                    DestinationAirportCode = f.DestinationCity?.AirportCode ?? "",
                    DepartureTime = departure,
                    ArrivalTime = arrival,
                    Duration = f.Duration,
                    AircraftType = f.AircraftType,
                    AvailableSeats = availableSeats,
                    BasePrice = basePrice,
                    FinalPrice = basePrice * classMultiplier * timingMultiplier * search.Passengers,
                    ScheduleID = schedule?.ScheduleID
                });
            }

            var orderedResults = flightResults.OrderBy(f => f.DepartureTime).ToList();
            
            // Apply pagination
            var totalResults = orderedResults.Count;
            var pagedResults = orderedResults
                .Skip((search.Page - 1) * search.PageSize)
                .Take(search.PageSize)
                .ToList();

            var results = new FlightSearchResultViewModel
            {
                SearchCriteria = search,
                Flights = pagedResults,
                TotalResults = totalResults,
                CurrentPage = search.Page,
                PageSize = search.PageSize
            };

            return View("All", results);
        }

        // GET: Flight/Search (legacy links)
        // Old links and the home page "Search flights" buttons use /Flight/Search.
        // To keep that URL working, simply redirect to Index where the main search UI lives.
        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string? tripType)
        {
            // If round-trip or multi-city is requested, show SearchResults with empty results
            if (tripType == "RoundTrip" || tripType == "MultiCity")
            {
                await PopulateCityDropdowns();
                var emptyModel = new FlightSearchResultViewModel
                {
                    SearchCriteria = new FlightSearchViewModel 
                    { 
                        TripType = tripType,
                        TravelDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
                        Passengers = 1,
                        Class = "Economy"
                    }
                };
                return View("SearchResults", emptyModel);
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Flight/Search
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Search(FlightSearchViewModel model)
        {
            // Custom validation for RoundTrip and MultiCity
            if (model.TripType == "RoundTrip")
            {
                if (!model.OriginCityID.HasValue || model.OriginCityID.Value == 0)
                {
                    ModelState.AddModelError("OriginCityID", "Please select origin city for round-trip searches.");
                }
                if (!model.DestinationCityID.HasValue || model.DestinationCityID.Value == 0)
                {
                    ModelState.AddModelError("DestinationCityID", "Please select destination city for round-trip searches.");
                }
                if (!model.ReturnDate.HasValue)
                {
                    ModelState.AddModelError("ReturnDate", "Please select a return date for round-trip searches.");
                }
            }
            else if (model.TripType == "MultiCity")
            {
                // MultiCity requires specific legs with valid cities
                if (model.Legs == null || !model.Legs.Any())
                {
                    ModelState.AddModelError("", "Please add at least one leg for multi-city searches.");
                }
                else
                {
                    // Validate each leg has valid cities and dates
                    for (int i = 0; i < model.Legs.Count; i++)
                    {
                        var leg = model.Legs[i];
                        if (leg.OriginCityID <= 0)
                        {
                            ModelState.AddModelError($"Legs[{i}].OriginCityID", $"Please select an origin city for leg {i + 1}.");
                        }
                        if (leg.DestinationCityID <= 0)
                        {
                            ModelState.AddModelError($"Legs[{i}].DestinationCityID", $"Please select a destination city for leg {i + 1}.");
                        }
                        if (leg.TravelDate < DateOnly.FromDateTime(DateTime.Today))
                        {
                            ModelState.AddModelError($"Legs[{i}].TravelDate", $"Travel date for leg {i + 1} must be today or later.");
                        }
                    }
                }
            }
            // OneWay can have null cities for "All Origins/Destinations"

            if (!ModelState.IsValid)
            {
                await PopulateCityDropdowns();
                return View("Index", model);
            }
            
            var results = new FlightSearchResultViewModel { SearchCriteria = model };

            // Helper to perform a single-leg search and return result list
            async Task<List<FlightResultItem>> DoLegSearchAsync(int originCityId, int destinationCityId, DateOnly travelDate)
            {
                var flights = await _context.Flights
                    .Include(f => f.OriginCity)
                    .Include(f => f.DestinationCity)
                    .Include(f => f.Schedules)
                    .Where(f => f.OriginCityID == originCityId && f.DestinationCityID == destinationCityId && f.Schedules.Any(s => s.Date == travelDate))
                    .ToListAsync();

                var legResults = new List<FlightResultItem>();
                foreach (var f in flights)
                {
                    var schedule = f.Schedules.FirstOrDefault(s => s.Date == travelDate);
                    if (schedule == null) continue;
                    
                    var departure = schedule.Date.ToDateTime(TimeOnly.FromDateTime(f.DepartureTime));
                    var arrival = schedule.Date.ToDateTime(TimeOnly.FromDateTime(f.ArrivalTime));

                    // Calculate available seats from FlightSeats table
                    await _seatService.GenerateFlightSeatsAsync(schedule.ScheduleID);
                    var reservedCount = await _context.FlightSeats
                        .Where(fs => fs.ScheduleId == schedule.ScheduleID && fs.Status == FlightSeatStatus.Reserved)
                        .CountAsync();
                    var availableSeats = f.TotalSeats - reservedCount;
                    
                    if (availableSeats < model.Passengers) continue;

                    var basePrice = f.BaseFare;
                    var classMultiplier = model.Class switch
                    {
                        "Business" => 2.0m,
                        "First" => 3.5m,
                        _ => 1.0m
                    };
                    var daysBeforeDeparture = (travelDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
                    var timingMultiplier = daysBeforeDeparture switch
                    {
                        >= 30 => 0.80m,
                        >= 15 => 1.00m,
                        >= 7 => 1.20m,
                        _ => 1.50m
                    };

                    legResults.Add(new FlightResultItem
                    {
                        FlightID = f.FlightID,
                        FlightNumber = f.FlightNumber,
                        OriginCity = f.OriginCity?.CityName ?? string.Empty,
                        OriginAirportCode = f.OriginCity?.AirportCode ?? string.Empty,
                        DestinationCity = f.DestinationCity?.CityName ?? string.Empty,
                        DestinationAirportCode = f.DestinationCity?.AirportCode ?? string.Empty,
                        DepartureTime = departure,
                        ArrivalTime = arrival,
                        Duration = f.Duration,
                        AircraftType = f.AircraftType,
                        AvailableSeats = availableSeats,
                        BasePrice = basePrice,
                        FinalPrice = basePrice * classMultiplier * timingMultiplier * model.Passengers,
                        ScheduleID = schedule.ScheduleID
                    });
                }
                
                return legResults;
            }

            if (model.TripType == "RoundTrip")
            {
                if (model.OutboundPage < 1) model.OutboundPage = 1;
                if (model.ReturnPage < 1) model.ReturnPage = 1;

                var outboundResults = await DoLegSearchAsync(model.OriginCityID!.Value, model.DestinationCityID!.Value, model.TravelDate);
                var returnResults = await DoLegSearchAsync(model.DestinationCityID!.Value, model.OriginCityID!.Value, model.ReturnDate!.Value);

                // Apply pagination
                results.OutboundTotalResults = outboundResults.Count;
                results.OutboundCurrentPage = model.OutboundPage;
                results.OutboundFlights = outboundResults
                    .Skip((model.OutboundPage - 1) * results.OutboundPageSize)
                    .Take(results.OutboundPageSize)
                    .ToList();

                results.ReturnTotalResults = returnResults.Count;
                results.ReturnCurrentPage = model.ReturnPage;
                results.ReturnFlights = returnResults
                    .Skip((model.ReturnPage - 1) * results.ReturnPageSize)
                    .Take(results.ReturnPageSize)
                    .ToList();
            }
            else if (model.TripType == "MultiCity" && model.Legs != null && model.Legs.Any())
            {
                foreach (var leg in model.Legs)
                {
                    var legResults = await DoLegSearchAsync(leg.OriginCityID, leg.DestinationCityID, leg.TravelDate);
                    results.LegsResults.Add(legResults);
                }
            }
            else // OneWay - redirect to Index with query parameters for pagination support
            {
                return RedirectToAction(nameof(Index), new 
                { 
                    OriginCityID = model.OriginCityID,
                    DestinationCityID = model.DestinationCityID,
                    TravelDate = model.TravelDate.ToString("yyyy-MM-dd"),
                    Passengers = model.Passengers,
                    Class = model.Class,
                    TripType = model.TripType,
                    Page = 1
                });
            }

            await PopulateCityDropdowns();
            return View("SearchResults", results);
        }

        // Preserve old /Flight/All links: redirect permanently to /Flight keeping the same query string
        [HttpGet]
        public IActionResult All()
        {
            var qs = Request?.QueryString.HasValue == true ? Request.QueryString.Value : string.Empty;
            return RedirectPermanent($"/Flight{qs}");
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