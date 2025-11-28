using Microsoft.AspNetCore.Mvc;
using ARS.Data;
using ARS.Models;

namespace ARS.Controllers
{
    public class SeedController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SeedController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Seed/Flights
        public async Task<IActionResult> Flights()
        {
            // Check if flights already exist
            if (_context.Flights.Any())
            {
                return Content("Flights already exist in the database.");
            }

            var flights = new List<Flight>
            {
                // Manila to Cebu
                new Flight
                {
                    FlightNumber = "ARS101",
                    OriginCityID = 1, // Manila
                    DestinationCityID = 2, // Cebu
                    DepartureTime = DateTime.Today.AddHours(8),
                    ArrivalTime = DateTime.Today.AddHours(9).AddMinutes(20),
                    Duration = 80,
                    AircraftType = "Airbus A320",
                    TotalSeats = 180,
                    BaseFare = 2500.00m,
                    PolicyID = 2
                },
                new Flight
                {
                    FlightNumber = "ARS102",
                    OriginCityID = 1, // Manila
                    DestinationCityID = 2, // Cebu
                    DepartureTime = DateTime.Today.AddHours(14),
                    ArrivalTime = DateTime.Today.AddHours(15).AddMinutes(20),
                    Duration = 80,
                    AircraftType = "Boeing 737",
                    TotalSeats = 160,
                    BaseFare = 2800.00m,
                    PolicyID = 2
                },
                // Cebu to Manila
                new Flight
                {
                    FlightNumber = "ARS201",
                    OriginCityID = 2, // Cebu
                    DestinationCityID = 1, // Manila
                    DepartureTime = DateTime.Today.AddHours(10),
                    ArrivalTime = DateTime.Today.AddHours(11).AddMinutes(20),
                    Duration = 80,
                    AircraftType = "Airbus A320",
                    TotalSeats = 180,
                    BaseFare = 2500.00m,
                    PolicyID = 2
                },
                // Manila to Tokyo
                new Flight
                {
                    FlightNumber = "ARS301",
                    OriginCityID = 1, // Manila
                    DestinationCityID = 3, // Tokyo
                    DepartureTime = DateTime.Today.AddHours(22),
                    ArrivalTime = DateTime.Today.AddDays(1).AddHours(4),
                    Duration = 240,
                    AircraftType = "Boeing 777",
                    TotalSeats = 300,
                    BaseFare = 15000.00m,
                    PolicyID = 1
                },
                // Tokyo to Manila
                new Flight
                {
                    FlightNumber = "ARS401",
                    OriginCityID = 3, // Tokyo
                    DestinationCityID = 1, // Manila
                    DepartureTime = DateTime.Today.AddHours(18),
                    ArrivalTime = DateTime.Today.AddHours(22),
                    Duration = 240,
                    AircraftType = "Boeing 777",
                    TotalSeats = 300,
                    BaseFare = 14500.00m,
                    PolicyID = 1
                },
                // Manila to Singapore
                new Flight
                {
                    FlightNumber = "ARS501",
                    OriginCityID = 1, // Manila
                    DestinationCityID = 4, // Singapore
                    DepartureTime = DateTime.Today.AddHours(6),
                    ArrivalTime = DateTime.Today.AddHours(9).AddMinutes(30),
                    Duration = 210,
                    AircraftType = "Airbus A330",
                    TotalSeats = 250,
                    BaseFare = 8500.00m,
                    PolicyID = 2
                },
                // Singapore to Manila
                new Flight
                {
                    FlightNumber = "ARS601",
                    OriginCityID = 4, // Singapore
                    DestinationCityID = 1, // Manila
                    DepartureTime = DateTime.Today.AddHours(11),
                    ArrivalTime = DateTime.Today.AddHours(14).AddMinutes(30),
                    Duration = 210,
                    AircraftType = "Airbus A330",
                    TotalSeats = 250,
                    BaseFare = 8500.00m,
                    PolicyID = 2
                },
                // Manila to Hong Kong
                new Flight
                {
                    FlightNumber = "ARS701",
                    OriginCityID = 1, // Manila
                    DestinationCityID = 5, // Hong Kong
                    DepartureTime = DateTime.Today.AddHours(12),
                    ArrivalTime = DateTime.Today.AddHours(14).AddMinutes(30),
                    Duration = 150,
                    AircraftType = "Airbus A321",
                    TotalSeats = 200,
                    BaseFare = 6500.00m,
                    PolicyID = 2
                },
                // Hong Kong to Manila
                new Flight
                {
                    FlightNumber = "ARS801",
                    OriginCityID = 5, // Hong Kong
                    DestinationCityID = 1, // Manila
                    DepartureTime = DateTime.Today.AddHours(16),
                    ArrivalTime = DateTime.Today.AddHours(18).AddMinutes(30),
                    Duration = 150,
                    AircraftType = "Airbus A321",
                    TotalSeats = 200,
                    BaseFare = 6500.00m,
                    PolicyID = 2
                }
            };

            _context.Flights.AddRange(flights);
            await _context.SaveChangesAsync();

            return Content($"Successfully seeded {flights.Count} flights to the database!");
        }
    }
}
