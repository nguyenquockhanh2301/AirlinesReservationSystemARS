using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ARS.Data;
using ARS.Models;
using ARS.ViewModels;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json;

namespace ARS.Controllers
{
    public class ReservationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ARS.Services.ISeatService _seatService;
        private readonly ARS.Services.IEmailService _emailService;

        public ReservationController(ApplicationDbContext context, UserManager<User> userManager, ARS.Services.ISeatService seatService, ARS.Services.IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _seatService = seatService;
            _emailService = emailService;
        }

        // GET: Reservation/Create
        // Supports both single-flight and multi-leg bookings
        // For single: flightId, scheduleId, travelDate
        // For multi-leg: flightIds (comma-separated), scheduleIds (comma-separated), travelDates (comma-separated)
        public async Task<IActionResult> Create(
            int flightId = 0, 
            int? scheduleId = null, 
            DateOnly? travelDate = null, 
            string? flightIds = null, 
            string? scheduleIds = null, 
            string? travelDates = null,
            int numAdults = 1, 
            string classType = "Economy")
        {
            // Check if user is logged in via Identity
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "Please login to book a flight.";
                var returnUrl = !string.IsNullOrEmpty(flightIds) 
                    ? $"/Reservation/Create?flightIds={flightIds}&scheduleIds={scheduleIds}&travelDates={travelDates}&numAdults={numAdults}&classType={classType}"
                    : $"/Reservation/Create?flightId={flightId}&scheduleId={scheduleId}&travelDate={travelDate}&numAdults={numAdults}&classType={classType}";
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            var user = currentUser;
            var model = new BookingViewModel
            {
                NumAdults = numAdults,
                Class = classType,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone
            };

            // Determine if this is a multi-leg or single-leg booking
            bool isMultiLeg = !string.IsNullOrEmpty(flightIds);

            if (isMultiLeg)
            {
                // Parse comma-separated values
                var flightIdList = flightIds!.Split(',').Select(int.Parse).ToList();
                var scheduleIdList = scheduleIds?.Split(',').Select(s => string.IsNullOrEmpty(s) ? (int?)null : int.Parse(s)).ToList() ?? new List<int?>();
                var travelDateList = travelDates!.Split(',').Select(DateOnly.Parse).ToList();

                if (flightIdList.Count != travelDateList.Count)
                {
                    return BadRequest("Mismatch between number of flights and travel dates.");
                }

                decimal totalPrice = 0;
                var classMultiplier = classType switch
                {
                    "Business" => 2.0m,
                    "First" => 3.5m,
                    _ => 1.0m
                };

                // Load all flights and create leg view models
                for (int i = 0; i < flightIdList.Count; i++)
                {
                    var flight = await _context.Flights
                        .Include(f => f.OriginCity)
                        .Include(f => f.DestinationCity)
                        .FirstOrDefaultAsync(f => f.FlightID == flightIdList[i]);

                    if (flight == null)
                    {
                        return NotFound($"Flight with ID {flightIdList[i]} not found.");
                    }

                    var legTravelDate = travelDateList[i];
                    var daysBeforeDeparture = (legTravelDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
                    var timingMultiplier = daysBeforeDeparture switch
                    {
                        >= 30 => 0.80m,
                        >= 15 => 1.00m,
                        >= 7 => 1.20m,
                        _ => 1.50m
                    };

                    var basePrice = flight.BaseFare * classMultiplier * timingMultiplier;
                    totalPrice += basePrice * numAdults;

                    var leg = new BookingLegViewModel
                    {
                        FlightID = flight.FlightID,
                        ScheduleID = i < scheduleIdList.Count ? scheduleIdList[i] : null,
                        FlightNumber = flight.FlightNumber,
                        Origin = flight.OriginCity?.CityName ?? "",
                        Destination = flight.DestinationCity?.CityName ?? "",
                        DepartureTime = flight.DepartureTime,
                        ArrivalTime = flight.ArrivalTime,
                        TravelDate = legTravelDate,
                        BasePrice = basePrice,
                        LegOrder = i + 1,
                        Duration = flight.Duration,
                        AircraftType = flight.AircraftType
                    };

                    model.Legs.Add(leg);
                }

                model.TotalPrice = totalPrice;
            }
            else
            {
                // Single-flight booking (legacy path)
                if (flightId == 0 || !travelDate.HasValue)
                {
                    return BadRequest("Flight ID and travel date are required.");
                }

                var flight = await _context.Flights
                    .Include(f => f.OriginCity)
                    .Include(f => f.DestinationCity)
                    .FirstOrDefaultAsync(f => f.FlightID == flightId);

                if (flight == null)
                {
                    return NotFound();
                }

                // Calculate pricing
                var daysBeforeDeparture = (travelDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
                var timingMultiplier = daysBeforeDeparture switch
                {
                    >= 30 => 0.80m,
                    >= 15 => 1.00m,
                    >= 7 => 1.20m,
                    _ => 1.50m
                };

                var classMultiplier = classType switch
                {
                    "Business" => 2.0m,
                    "First" => 3.5m,
                    _ => 1.0m
                };

                var basePrice = flight.BaseFare * classMultiplier * timingMultiplier;

                model.FlightID = flightId;
                model.ScheduleID = scheduleId;
                model.FlightNumber = flight.FlightNumber;
                model.Origin = flight.OriginCity?.CityName ?? "";
                model.Destination = flight.DestinationCity?.CityName ?? "";
                model.DepartureTime = flight.DepartureTime;
                model.ArrivalTime = flight.ArrivalTime;
                model.TravelDate = travelDate.Value;
                model.BasePrice = basePrice;
                model.TotalPrice = basePrice * numAdults;
            }

            return View(model);
        }

        // POST: Reservation/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingViewModel model)
        {
                        // Try to parse seat assignments JSON (for single-leg multi-seat support)
                        List<(int flightSeatId, string seatLabel, string? passengerType)> parsedSeatAssignments = new();
                        try
                        {
                            var seatJson = Request.Form["SeatAssignmentsJson"].FirstOrDefault();
                            if (!string.IsNullOrWhiteSpace(seatJson))
                            {
                                using var doc = JsonDocument.Parse(seatJson);
                                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                                {
                                    if (doc.RootElement.TryGetProperty("seats", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var item in seatsEl.EnumerateArray())
                                        {
                                            var fsId = item.TryGetProperty("flightSeatId", out var idEl) && idEl.TryGetInt32(out var idVal) ? idVal : 0;
                                            var seatLabel = item.TryGetProperty("seatLabel", out var slEl) ? slEl.GetString() ?? string.Empty : string.Empty;
                                            var pType = item.TryGetProperty("passengerType", out var ptEl) ? ptEl.GetString() : null;
                                            if (fsId > 0 && !string.IsNullOrWhiteSpace(seatLabel))
                                            {
                                                parsedSeatAssignments.Add((fsId, seatLabel.Trim(), pType));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Ignore JSON parse errors; fall back to legacy path
                        }
            // Check if user is logged in via Identity
            var currentUserPost = await _userManager.GetUserAsync(User);
            if (currentUserPost == null)
            {
                TempData["ErrorMessage"] = "Please login to book a flight.";
                return RedirectToAction("Login", "Account");
            }

            // For single-leg bookings, enforce model validation strictly.
            // For multi-leg bookings, key fields are populated server-side, so we allow
            // some binding quirks (e.g. collection item binding) and proceed.
            if (!ModelState.IsValid && !model.IsMultiLeg)
            {
                return View(model);
            }

            var user = currentUserPost;

            // Use transaction to ensure atomicity for bookings
            using var transaction = await _context.Database.BeginTransactionAsync();
            var transactionCommitted = false;
            
            // Track FlightSeatId for single-leg bookings (used after commit)
            int? singleLegFlightSeatId = null;
            
            try
            {
                var reservationsToAdd = new List<Reservation>();
                bool isMultiLeg = model.IsMultiLeg;
                Reservation? parentReservation = null;

                if (isMultiLeg)
                {
                    // Create a single parent reservation and attach ReservationLeg entries
                    var orderedLegs = (model.Legs ?? new List<BookingLegViewModel>())
                        .OrderBy(l => l.LegOrder)
                        .ToList();

                    if (!orderedLegs.Any())
                    {
                        ModelState.AddModelError(string.Empty, "No legs were provided for a multi-leg booking.");
                        await transaction.RollbackAsync();
                        return View(model);
                    }

                    parentReservation = new Reservation
                    {
                        UserID = user.Id,
                        BookingDate = DateOnly.FromDateTime(DateTime.Now),
                        Status = "Pending",
                        NumAdults = model.NumAdults,
                        NumChildren = model.NumChildren,
                        NumSeniors = model.NumSeniors,
                        Class = model.Class,
                        ConfirmationNumber = GenerateConfirmationNumber(),
                        BlockingNumber = GenerateBlockingNumber()
                    };

                    _context.Reservations.Add(parentReservation);
                    await _context.SaveChangesAsync();

                    // Calculate total price for multi-leg journey and create initial payment
                    decimal totalMultiLegPrice = 0;
                    var passengers = model.NumAdults + model.NumChildren + model.NumSeniors;
                    
                    foreach (var leg in orderedLegs)
                    {
                        var flight = await _context.Flights.FindAsync(leg.FlightID);
                        if (flight != null)
                        {
                            var daysBefore = (leg.TravelDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
                            var timingMultiplier = daysBefore switch
                            {
                                >= 30 => 0.80m,
                                >= 15 => 1.00m,
                                >= 7 => 1.20m,
                                _ => 1.50m
                            };
                            
                            var classMultiplier = model.Class switch
                            {
                                "Business" => 2.0m,
                                "First" => 3.5m,
                                _ => 1.0m
                            };
                            
                            totalMultiLegPrice += flight.BaseFare * classMultiplier * timingMultiplier * passengers;
                        }
                    }
                    
                    if (totalMultiLegPrice > 0)
                    {
                        var initialPayment = new Payment
                        {
                            ReservationID = parentReservation.ReservationID,
                            Amount = Math.Round(totalMultiLegPrice, 2),
                            PaymentDate = DateTime.Now,
                            PaymentMethod = "Pending",
                            TransactionStatus = "Pending",
                            TransactionRefNo = null
                        };
                        _context.Payments.Add(initialPayment);
                        await _context.SaveChangesAsync();
                    }

                    var legsToAdd = new List<ReservationLeg>();

                    foreach (var leg in orderedLegs)
                    {
                        var flight = await _context.Flights.FindAsync(leg.FlightID);
                        if (flight == null)
                        {
                            ModelState.AddModelError(string.Empty, $"Flight {leg.FlightNumber} not found.");
                            await transaction.RollbackAsync();
                            return View(model);
                        }

                        // Enforce booking cutoff for this leg
                        var departureDateTime = leg.TravelDate.ToDateTime(TimeOnly.FromDateTime(flight.DepartureTime));
                        var cutoff = departureDateTime.AddMinutes(-60);
                        if (DateTime.Now >= cutoff)
                        {
                            ModelState.AddModelError(string.Empty, $"Bookings for flight {leg.FlightNumber} are closed 60 minutes before departure.");
                            await transaction.RollbackAsync();
                            return View(model);
                        }

                        // Create or get schedule for this leg
                        var schedule = await _context.Schedules
                            .FirstOrDefaultAsync(s => s.FlightID == leg.FlightID && s.Date == leg.TravelDate);

                        if (schedule == null)
                        {
                            schedule = new Schedule
                            {
                                FlightID = leg.FlightID,
                                Date = leg.TravelDate,
                                Status = "Scheduled"
                            };
                            _context.Schedules.Add(schedule);
                            await _context.SaveChangesAsync();
                        }

                        // For multi-leg: leg.SelectedSeatId now contains FlightSeatId (from the seat map)
                        int? flightSeatIdForLeg = leg.SelectedSeatId; // This is actually FlightSeatId from the UI
                        Seat? seatForLeg = null;
                        
                        if (!string.IsNullOrEmpty(leg.SelectedSeat))
                        {
                            // Look up by label (most reliable for multi-leg bookings)
                            seatForLeg = await _context.Seats
                                .FirstOrDefaultAsync(s => s.SeatLayoutId == flight.SeatLayoutId && s.Label == leg.SelectedSeat);
                        }

                        if (seatForLeg != null)
                        {
                            if (flight.SeatLayoutId.HasValue && seatForLeg.SeatLayoutId != flight.SeatLayoutId)
                            {
                                ModelState.AddModelError("SelectedSeat", $"Selected seat is not valid for flight {leg.FlightNumber}.");
                                await transaction.RollbackAsync();
                                return View(model);
                            }

                            // Check if the FlightSeat is already reserved
                            if (flightSeatIdForLeg.HasValue)
                            {
                                var flightSeatTaken = await _context.FlightSeats
                                    .AnyAsync(fs => fs.FlightSeatId == flightSeatIdForLeg.Value && 
                                                   fs.Status == FlightSeatStatus.Reserved);
                                
                                if (flightSeatTaken)
                                {
                                    ModelState.AddModelError("SelectedSeat", $"Seat {seatForLeg.Label} on flight {leg.FlightNumber} has already been booked.");
                                    await transaction.RollbackAsync();
                                    return View(model);
                                }
                            }
                        }

                        var reservationLeg = new ReservationLeg
                        {
                            ReservationID = parentReservation.ReservationID,
                            FlightID = leg.FlightID,
                            ScheduleID = schedule.ScheduleID,
                            TravelDate = leg.TravelDate,
                            LegOrder = leg.LegOrder,
                            SeatId = seatForLeg?.SeatId, // Store the actual Seat.SeatId for FK constraint
                            FlightSeatId = flightSeatIdForLeg, // Store FlightSeatId here (will be used for reservation)
                            SeatLabel = seatForLeg?.Label
                        };

                        legsToAdd.Add(reservationLeg);
                    }

                    _context.ReservationLegs.AddRange(legsToAdd);
                    await _context.SaveChangesAsync();
                    
                    // Send booking confirmation email for multi-leg after legs are created
                    try
                    {
                        Console.WriteLine($"[BOOKING DEBUG] Attempting to send multi-leg booking email...");
                        if (parentReservation != null && totalMultiLegPrice > 0)
                        {
                            Console.WriteLine($"[BOOKING DEBUG] Building email for reservation {parentReservation.ReservationID}");
                            var emailBody = BuildBookingConfirmationEmail(parentReservation, user, legsToAdd, totalMultiLegPrice);
                            Console.WriteLine($"[BOOKING DEBUG] Email body built, sending to {user.Email}");
                            await _emailService.SendAsync(user.Email, "Booking Confirmation - ARS Airlines", emailBody);
                            Console.WriteLine($"[BOOKING DEBUG] Email send method completed");
                        }
                        else
                        {
                            Console.WriteLine($"[BOOKING DEBUG] Skipped email - parentReservation null: {parentReservation == null}, totalPrice: {totalMultiLegPrice}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BOOKING ERROR] Failed to send booking confirmation email: {ex.Message}");
                        Console.WriteLine($"[BOOKING ERROR] Stack trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    // Single-leg booking (existing path)
                    var flight = await _context.Flights.FindAsync(model.FlightID);
                    if (flight == null)
                    {
                        ModelState.AddModelError(string.Empty, "Flight not found.");
                        await transaction.RollbackAsync();
                        return View(model);
                    }

                    // Enforce booking cutoff: bookings are closed 60 minutes before departure
                    try
                    {
                        var departureDateTime = model.TravelDate.ToDateTime(TimeOnly.FromDateTime(flight.DepartureTime));
                        var cutoff = departureDateTime.AddMinutes(-60);
                        if (DateTime.Now >= cutoff)
                        {
                            ModelState.AddModelError(string.Empty, "Bookings for this flight are closed 60 minutes before departure.");
                            await transaction.RollbackAsync();
                            return View(model);
                        }
                    }
                    catch
                    {
                        // if any error computing cutoff, proceed conservatively (do not block)
                    }

                    // Create or get schedule
                    var schedule = await _context.Schedules
                        .FirstOrDefaultAsync(s => s.FlightID == model.FlightID && s.Date == model.TravelDate);

                    if (schedule == null)
                    {
                        schedule = new Schedule
                        {
                            FlightID = model.FlightID,
                            Date = model.TravelDate,
                            Status = "Scheduled"
                        };
                        _context.Schedules.Add(schedule);
                        await _context.SaveChangesAsync();
                    }

                    var reservationForLeg = new Reservation
                    {
                        UserID = user.Id,
                        BookingDate = DateOnly.FromDateTime(DateTime.Now),
                        Status = "Pending",
                        NumAdults = model.NumAdults,
                        NumChildren = model.NumChildren,
                        NumSeniors = model.NumSeniors,
                        Class = model.Class,
                        ConfirmationNumber = GenerateConfirmationNumber(),
                        BlockingNumber = GenerateBlockingNumber(),
                        FlightID = model.FlightID,
                        ScheduleID = schedule.ScheduleID,
                        TravelDate = model.TravelDate
                    };

                    // If client submitted multiple seat assignments, normalize counts and class now
                    if (parsedSeatAssignments.Any())
                    {
                        // Re-count passengers by passengerType if provided
                        var a = 0; var c = 0; var s = 0;
                        foreach (var p in parsedSeatAssignments)
                        {
                            if (string.Equals(p.passengerType, "child", StringComparison.OrdinalIgnoreCase)) c++;
                            else if (string.Equals(p.passengerType, "senior", StringComparison.OrdinalIgnoreCase)) s++;
                            else a++;
                        }
                        reservationForLeg.NumAdults = a;
                        reservationForLeg.NumChildren = c;
                        reservationForLeg.NumSeniors = s;
                        // Update reservation class to the highest among selected seats later when we load seats
                    }
                    else
                    {
                        // Legacy single-seat path
                        singleLegFlightSeatId = model.SelectedSeatId; // Store for later use
                        Seat? selectedSeat = null;
                        
                        if (!string.IsNullOrEmpty(model.SelectedSeat))
                        {
                            // Look up the actual Seat by label
                            selectedSeat = await _context.Seats
                                .FirstOrDefaultAsync(s => s.SeatLayoutId == flight.SeatLayoutId && s.Label == model.SelectedSeat);
                            
                            if (selectedSeat == null)
                            {
                                ModelState.AddModelError("SelectedSeat", "Selected seat not found.");
                                await transaction.RollbackAsync();
                                return View(model);
                            }

                            // Ensure the seat belongs to the flight's seat layout
                            if (flight.SeatLayoutId.HasValue && selectedSeat.SeatLayoutId != flight.SeatLayoutId)
                            {
                                ModelState.AddModelError("SelectedSeat", "Selected seat is not valid for this flight.");
                                await transaction.RollbackAsync();
                                return View(model);
                            }

                            // Check if the FlightSeat is already reserved
                            if (singleLegFlightSeatId.HasValue)
                            {
                                var flightSeatTaken = await _context.FlightSeats
                                    .AnyAsync(fs => fs.FlightSeatId == singleLegFlightSeatId.Value && 
                                                   fs.Status == FlightSeatStatus.Reserved);
                                
                                if (flightSeatTaken)
                                {
                                    ModelState.AddModelError("SelectedSeat", "That seat has just been booked by someone else. Please choose a different seat.");
                                    await transaction.RollbackAsync();
                                    return View(model);
                                }
                            }

                            reservationForLeg.SeatId = selectedSeat.SeatId;
                            reservationForLeg.SeatLabel = selectedSeat.Label;
                        }
                    }

                    reservationsToAdd.Add(reservationForLeg);
                }

                _context.Reservations.AddRange(reservationsToAdd);

                // Create initial pending payments for all reservations
                foreach (var reservation in reservationsToAdd)
                {
                    // Calculate the total price for this reservation
                    var flight = await _context.Flights.FindAsync(reservation.FlightID);
                    if (flight != null)
                    {
                        decimal totalPrice;
                        var travelDate = reservation.TravelDate;
                        var daysBefore = (travelDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
                        var timingMultiplier = daysBefore switch
                        {
                            >= 30 => 0.80m,
                            >= 15 => 1.00m,
                            >= 7 => 1.20m,
                            _ => 1.50m
                        };

                        if (!model.IsMultiLeg && parsedSeatAssignments.Any())
                        {
                            // Seat-based pricing for single-leg multi-seat selection
                            totalPrice = 0m;
                            var firstSeatLabel = new List<string>();
                            var highestClass = CabinClass.Economy;
                            foreach (var (fsId, seatLabel, _) in parsedSeatAssignments)
                            {
                                var fs = await _context.FlightSeats.Include(x => x.AircraftSeat).FirstOrDefaultAsync(x => x.FlightSeatId == fsId);
                                if (fs?.AircraftSeat != null)
                                {
                                    var seatClassMultiplier = fs.AircraftSeat.CabinClass switch
                                    {
                                        CabinClass.Business => 2.0m,
                                        CabinClass.First => 3.5m,
                                        _ => 1.0m
                                    };
                                    totalPrice += flight.BaseFare * seatClassMultiplier * timingMultiplier;
                                    firstSeatLabel.Add(seatLabel);
                                    // Choose the most premium class (First > Business > Economy). Enum order is First=0, Business=1, Economy=2.
                                    if (fs.AircraftSeat.CabinClass < highestClass) highestClass = fs.AircraftSeat.CabinClass;
                                }
                            }
                            // Update reservation class and seat label to reflect selection summary
                            reservation.Class = highestClass switch
                            {
                                CabinClass.First => "First",
                                CabinClass.Business => "Business",
                                _ => "Economy"
                            };
                            if (firstSeatLabel.Any()) reservation.SeatLabel = string.Join(", ", firstSeatLabel);
                            totalPrice = Math.Round(totalPrice, 2);
                        }
                        else
                        {
                            var passengers = reservation.NumAdults + reservation.NumChildren + reservation.NumSeniors;
                            var classMultiplier = reservation.Class switch
                            {
                                "Business" => 2.0m,
                                "First" => 3.5m,
                                _ => 1.0m
                            };
                            totalPrice = Math.Round(flight.BaseFare * classMultiplier * timingMultiplier * passengers, 2);
                        }

                        var initialPayment = new Payment
                        {
                            ReservationID = reservation.ReservationID,
                            Amount = totalPrice,
                            PaymentDate = DateTime.Now,
                            PaymentMethod = "Pending",
                            TransactionStatus = "Pending",
                            TransactionRefNo = null
                        };

                        reservation.Payments = new List<Payment> { initialPayment };
                    }
                }

                // Ensure a legacy Users row exists for compatibility with existing FK in the database.
                try
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO `Users` (`UserID`, `FirstName`, `LastName`, `Email`, `Password`, `Phone`, `Address`, `Gender`, `Age`, `CreditCardNumber`, `SkyMiles`, `Role`)
SELECT {user.Id}, {user.FirstName}, {user.LastName}, {user.Email}, '', {user.Phone}, {user.Address}, {user.Gender.ToString()}, {user.Age}, {user.CreditCardNumber}, {user.SkyMiles}, 'Customer'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `Users` WHERE `UserID` = {user.Id});
" );
                }
                catch
                {
                    // If this insert fails (e.g. legacy Users table not present) continue
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                transactionCommitted = true;

                Reservation firstReservation;
                if (isMultiLeg)
                {
                    if (parentReservation == null)
                    {
                        // fallback: try to get a reservation created in this transaction
                        firstReservation = await _context.Reservations.OrderByDescending(r => r.ReservationID).FirstAsync();
                    }
                    else
                    {
                        firstReservation = parentReservation;
                    }
                }
                else
                {
                    firstReservation = reservationsToAdd.First();
                }

                // Preserve selected seat for single-leg path so it can be shown on the confirmation page.
                if (!model.IsMultiLeg && !string.IsNullOrEmpty(model.SelectedSeat))
                {
                    TempData["SelectedSeat"] = model.SelectedSeat;
                }

                // After committing the parent reservation, attempt to reserve corresponding FlightSeat rows
                try
                {
                    if (model.IsMultiLeg)
                    {
                        // multi-leg: find reservation legs and reserve matching flight seats
                        var legs = await _context.ReservationLegs
                            .Where(rl => rl.ReservationID == firstReservation.ReservationID)
                            .ToListAsync();

                        foreach (var rl in legs)
                        {
                            if (rl.FlightSeatId.HasValue)
                            {
                                // FlightSeatId contains the FlightSeat ID from the seat map selection
                                int flightSeatId = rl.FlightSeatId.Value;
                                await _seatService.ReserveSeatForLegAsync(flightSeatId, rl.ReservationLegID);
                            }
                        }
                    }
                    else
                    {
                        // single-leg: reserve one or multiple seats and create ReservationLeg entries
                        var res = await _context.Reservations.FindAsync(firstReservation.ReservationID);
                        if (res != null && res.ScheduleID.HasValue && res.FlightID.HasValue)
                        {
                            var legsToCreate = new List<ReservationLeg>();
                            if (parsedSeatAssignments.Any())
                            {
                                foreach (var (fsId, seatLabel, _) in parsedSeatAssignments)
                                {
                                    Seat? resSeat = null;
                                    var resFlight = await _context.Flights.FindAsync(res.FlightID.Value);
                                    if (resFlight?.SeatLayoutId != null)
                                    {
                                        resSeat = await _context.Seats
                                            .FirstOrDefaultAsync(s => s.SeatLayoutId == resFlight.SeatLayoutId && s.Label == seatLabel);
                                    }
                                    var leg = new ReservationLeg
                                    {
                                        ReservationID = res.ReservationID,
                                        FlightID = res.FlightID.Value,
                                        ScheduleID = res.ScheduleID.Value,
                                        TravelDate = res.TravelDate,
                                        SeatLabel = seatLabel,
                                        SeatId = resSeat?.SeatId,
                                        FlightSeatId = fsId,
                                        LegOrder = 1
                                    };
                                    legsToCreate.Add(leg);
                                }
                                _context.ReservationLegs.AddRange(legsToCreate);
                                await _context.SaveChangesAsync();

                                foreach (var leg in legsToCreate)
                                {
                                    if (leg.FlightSeatId.HasValue)
                                    {
                                        await _seatService.ReserveSeatForLegAsync(leg.FlightSeatId.Value, leg.ReservationLegID);
                                    }
                                }
                            }
                            else if (!string.IsNullOrEmpty(res.SeatLabel))
                            {
                                // Legacy single-seat behavior
                                // Look up the seat and create a single leg
                                var resFlight = await _context.Flights.FindAsync(res.FlightID.Value);
                                Seat? resSeat = null;
                                if (resFlight?.SeatLayoutId != null)
                                {
                                    resSeat = await _context.Seats
                                        .FirstOrDefaultAsync(s => s.SeatLayoutId == resFlight.SeatLayoutId && s.Label == res.SeatLabel);
                                }
                                var singleLeg = new ReservationLeg
                                {
                                    ReservationID = res.ReservationID,
                                    FlightID = res.FlightID.Value,
                                    ScheduleID = res.ScheduleID.Value,
                                    TravelDate = res.TravelDate,
                                    SeatLabel = res.SeatLabel,
                                    SeatId = resSeat?.SeatId,
                                    FlightSeatId = singleLegFlightSeatId,
                                    LegOrder = 1
                                };
                                _context.ReservationLegs.Add(singleLeg);
                                await _context.SaveChangesAsync();
                                if (singleLegFlightSeatId.HasValue)
                                {
                                    await _seatService.ReserveSeatForLegAsync(singleLegFlightSeatId.Value, singleLeg.ReservationLegID);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // If seat reservation fails here, it is non-fatal for the booking itself.
                    // The seat may still be reserved via legacy label logic or can be corrected by the user.
                }

                // Send booking confirmation email
                try
                {
                    Console.WriteLine($"[BOOKING DEBUG] Attempting to send single-leg booking email...");
                    var fullReservation = await _context.Reservations
                        .Include(r => r.Flight)
                            .ThenInclude(f => f!.OriginCity)
                        .Include(r => r.Flight)
                            .ThenInclude(f => f!.DestinationCity)
                        .Include(r => r.Legs)
                            .ThenInclude(l => l.Flight)
                                .ThenInclude(f => f!.OriginCity)
                        .Include(r => r.Legs)
                            .ThenInclude(l => l.Flight)
                                .ThenInclude(f => f!.DestinationCity)
                        .Include(r => r.Payments)
                        .FirstOrDefaultAsync(r => r.ReservationID == firstReservation.ReservationID);

                    if (fullReservation != null)
                    {
                        Console.WriteLine($"[BOOKING DEBUG] Found reservation {fullReservation.ReservationID}");
                        var totalAmount = fullReservation.Payments?.FirstOrDefault()?.Amount ?? 0;
                        string emailBody;
                        
                        if (fullReservation.Legs != null && fullReservation.Legs.Any())
                        {
                            Console.WriteLine($"[BOOKING DEBUG] Building multi-leg email for {fullReservation.Legs.Count} legs");
                            emailBody = BuildBookingConfirmationEmail(fullReservation, user, fullReservation.Legs.ToList(), totalAmount);
                            Console.WriteLine($"[BOOKING DEBUG] Sending multi-leg email to {user.Email}");
                            await _emailService.SendAsync(user.Email, "Booking Confirmation - ARS Airlines", emailBody);
                        }
                        else
                        {
                            Console.WriteLine($"[BOOKING DEBUG] Single-leg booking detected");
                            var flight = fullReservation.Flight ?? await _context.Flights
                                .Include(f => f.OriginCity)
                                .Include(f => f.DestinationCity)
                                .FirstOrDefaultAsync(f => f.FlightID == fullReservation.FlightID);
                            
                            if (flight != null)
                            {
                                Console.WriteLine($"[BOOKING DEBUG] Building single-leg email");
                                emailBody = BuildBookingConfirmationEmailSingle(fullReservation, user, flight, totalAmount);
                                Console.WriteLine($"[BOOKING DEBUG] Sending single-leg email to {user.Email}");
                                await _emailService.SendAsync(user.Email, "Booking Confirmation - ARS Airlines", emailBody);
                            }
                            else
                            {
                                Console.WriteLine($"[BOOKING DEBUG] Flight not found for single-leg email");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[BOOKING DEBUG] Full reservation not found");
                    }
                }
                catch (Exception ex)
                {
                    // Don't fail the booking if email fails
                    Console.WriteLine($"[BOOKING ERROR] Failed to send booking confirmation email: {ex.Message}");
                    Console.WriteLine($"[BOOKING ERROR] Stack trace: {ex.StackTrace}");
                }

                return RedirectToAction(nameof(Confirmation), new { id = firstReservation.ReservationID });
            }
            catch (Exception ex)
            {
                if (!transactionCommitted)
                {
                    try { await transaction.RollbackAsync(); } catch { }
                }

                // Surface inner exception details to help diagnose data/constraint issues (e.g. DB update errors).
                var detailedMessage = ex.InnerException != null
                    ? $"{ex.Message} Inner: {ex.InnerException.Message}"
                    : ex.Message;

                ModelState.AddModelError(string.Empty, $"An error occurred while creating the reservation: {detailedMessage}");
                return View(model);
            }
        }

        // GET: Reservation/Confirmation/5
        public async Task<IActionResult> Confirmation(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Flight)
                    .ThenInclude(f => f.OriginCity)
                .Include(r => r.Flight)
                    .ThenInclude(f => f.DestinationCity)
                .Include(r => r.Schedule)
                .Include(r => r.Legs)
                    .ThenInclude(rl => rl.Flight)
                        .ThenInclude(f => f.OriginCity)
                .Include(r => r.Legs)
                    .ThenInclude(rl => rl.Flight)
                        .ThenInclude(f => f.DestinationCity)
                .FirstOrDefaultAsync(m => m.ReservationID == id);

            if (reservation == null)
            {
                return NotFound();
            }

            return View(reservation);
        }

            // GET: Reservation/GetSeatMap?flightId=1&travelDate=2025-11-16
            [HttpGet]
            public async Task<IActionResult> GetSeatMap(int flightId, DateOnly travelDate, int? scheduleId = null)
            {
                var flight = await _context.Flights
                    .FirstOrDefaultAsync(f => f.FlightID == flightId);

                if (flight == null) return NotFound();

                // Ensure flight has a SeatLayout - if not, assign the default one (ID=1)
                if (!flight.SeatLayoutId.HasValue)
                {
                    // Check if default SeatLayout exists
                    var defaultLayout = await _context.SeatLayouts.FirstOrDefaultAsync();
                    if (defaultLayout != null)
                    {
                        flight.SeatLayoutId = defaultLayout.SeatLayoutId;
                        _context.Flights.Update(flight);
                        await _context.SaveChangesAsync();
                        System.Diagnostics.Debug.WriteLine($"Assigned SeatLayout {defaultLayout.SeatLayoutId} to Flight {flightId}");
                    }
                    else
                    {
                        return NotFound(new { error = "No seat layouts configured in the system." });
                    }
                }

                var seats = await _context.Seats
                    .Where(s => s.SeatLayoutId == flight.SeatLayoutId.Value)
                    .OrderBy(s => s.RowNumber)
                    .ThenBy(s => s.Column)
                    .ToListAsync();

                // find schedule for the date (if exists)
                Schedule? schedule = null;
                if (scheduleId.HasValue)
                {
                    schedule = await _context.Schedules.FirstOrDefaultAsync(s => s.ScheduleID == scheduleId.Value);
                }
                else
                {
                    schedule = await _context.Schedules.FirstOrDefaultAsync(s => s.FlightID == flightId && s.Date == travelDate);
                }

                // Ensure FlightSeats exist for this schedule
                if (schedule != null)
                {
                    await _seatService.GenerateFlightSeatsAsync(schedule.ScheduleID);
                }

                var reservedFlightSeatIds = new HashSet<int>();
                if (schedule != null)
                {
                    // Get reserved FlightSeat IDs (not Seat IDs)
                    var reservedFSIds = await _context.FlightSeats
                        .Where(fs => fs.ScheduleId == schedule.ScheduleID && 
                                    fs.Status == FlightSeatStatus.Reserved)
                        .Select(fs => fs.FlightSeatId)
                        .ToListAsync();

                    reservedFlightSeatIds = reservedFSIds.ToHashSet();
                }

                // Get all FlightSeats for this schedule to map Seat -> FlightSeat
                var flightSeatsMap = new Dictionary<int, int>();
                if (schedule != null)
                {
                    var allFlightSeats = await _context.FlightSeats
                        .Where(fs => fs.ScheduleId == schedule.ScheduleID)
                        .ToListAsync();
                    flightSeatsMap = allFlightSeats.ToDictionary(fs => fs.SeatId, fs => fs.FlightSeatId);
                }

                var rowsResult = seats
                    .GroupBy(s => s.RowNumber)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        row = g.Key,
                        seats = g.Select(s => new
                        {
                            id = flightSeatsMap.ContainsKey(s.SeatId) ? flightSeatsMap[s.SeatId] : 0,
                            label = s.Label,
                            col = s.Column,
                            row = s.RowNumber,
                            cabin = s.CabinClass.ToString(),
                            available = flightSeatsMap.ContainsKey(s.SeatId) && !reservedFlightSeatIds.Contains(flightSeatsMap[s.SeatId])
                        }).ToList()
                    }).ToList();

                // seats per row is variable; include max count
                var seatsPerRow = seats.GroupBy(s => s.RowNumber).Select(g => g.Count()).DefaultIfEmpty(0).Max();
                var rows = rowsResult.Count;
                
                System.Diagnostics.Debug.WriteLine($"GetSeatMap: Using SeatLayout {flight.SeatLayoutId} for flight {flightId}, schedule {schedule?.ScheduleID}");
                return Json(new { rows, seatsPerRow, rowsResult });
        }

        // GET: Reservation/MyReservations
        public async Task<IActionResult> MyReservations()
        {
            // Check if user is logged in via Identity
            var currentUserList = await _userManager.GetUserAsync(User);
            if (currentUserList == null)
            {
                TempData["ErrorMessage"] = "Please login to view your reservations.";
                return RedirectToAction("Login", "Account", new { returnUrl = "/Reservation/MyReservations" });
            }

            var reservations = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Flight)
                    .ThenInclude(f => f.OriginCity)
                .Include(r => r.Flight)
                    .ThenInclude(f => f.DestinationCity)
                .Include(r => r.Schedule)
                .Include(r => r.Legs)
                    .ThenInclude(rl => rl.Flight)
                        .ThenInclude(f => f.OriginCity)
                .Include(r => r.Legs)
                    .ThenInclude(rl => rl.Flight)
                        .ThenInclude(f => f.DestinationCity)
                .Where(r => r.UserID == currentUserList.Id)
                .OrderByDescending(r => r.BookingDate)
                .ToListAsync();

            return View(reservations);
        }

        // GET: Reservation/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Check if user is logged in via Identity
            var currentUserDetails = await _userManager.GetUserAsync(User);
            if (currentUserDetails == null)
            {
                TempData["ErrorMessage"] = "Please login to view reservation details.";
                return RedirectToAction("Login", "Account");
            }

            var reservation = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Flight)
                    .ThenInclude(f => f.OriginCity)
                .Include(r => r.Flight)
                    .ThenInclude(f => f.DestinationCity)
                .Include(r => r.Schedule)
                .Include(r => r.Legs)
                    .ThenInclude(rl => rl.Flight)
                        .ThenInclude(f => f.OriginCity)
                .Include(r => r.Legs)
                    .ThenInclude(rl => rl.Flight)
                        .ThenInclude(f => f.DestinationCity)
                .Include(r => r.Legs)
                    .ThenInclude(rl => rl.Schedule)
                .Include(r => r.Legs)
                    .ThenInclude(rl => rl.Seat)
                .Include(r => r.Payments)
                .Include(r => r.Refunds)
                .FirstOrDefaultAsync(m => m.ReservationID == id);

            if (reservation == null)
            {
                return NotFound();
            }

            // Verify the reservation belongs to the logged-in user OR user is an admin
            var isAdmin = User.IsInRole("Admin");
            if (reservation.UserID != currentUserDetails.Id && !isAdmin)
            {
                return Forbid();
            }

            return View(reservation);
        }

        // GET: Reservation/Reschedule/5
        public async Task<IActionResult> Reschedule(int? id)
        {
            if (id == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "Please login to reschedule a reservation.";
                return RedirectToAction("Login", "Account", new { returnUrl = $"/Reservation/Details/{id}" });
            }

            var reservation = await _context.Reservations
                .Include(r => r.Flight)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.ReservationID == id);

            if (reservation == null) return NotFound();
            
            // Allow access if user owns reservation OR user is admin
            var isAdmin = User.IsInRole("Admin");
            if (reservation.UserID != currentUser.Id && !isAdmin) return Forbid();

            var vm = new ARS.ViewModels.RescheduleInitViewModel
            {
                Reservation = reservation,
                NewDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1))
            };

            return View(vm);
        }

        // POST: Reservation/RescheduleSearch/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleSearch(int id, DateOnly newDate, int page = 1)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "Please login to reschedule a reservation.";
                return RedirectToAction("Login", "Account");
            }

            var reservation = await _context.Reservations
                .Include(r => r.Flight)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.ReservationID == id);

            if (reservation == null) return NotFound();
            
            // Allow access if user owns reservation OR user is admin
            var isAdmin = User.IsInRole("Admin");
            if (reservation.UserID != currentUser.Id && !isAdmin) return Forbid();

            var passengers = reservation.NumAdults + reservation.NumChildren + reservation.NumSeniors;

            // Find candidate flights matching the original route and available on the requested date
            var originCityId = reservation.Flight?.OriginCityID ?? 0;
            var destinationCityId = reservation.Flight?.DestinationCityID ?? 0;

            var flightsQuery = _context.Flights
                .Include(f => f.OriginCity)
                .Include(f => f.DestinationCity)
                .Include(f => f.Schedules)
                .Include(f => f.Reservations)
                .Where(f => f.OriginCityID == originCityId && f.DestinationCityID == destinationCityId);

            var flights = await flightsQuery.ToListAsync();

            var results = new List<ARS.ViewModels.FlightResultItem>();

            foreach (var f in flights)
            {
                var schedule = f.Schedules.FirstOrDefault(s => s.Date == newDate);
                if (schedule == null) continue;

                // Calculate available seats from FlightSeats table
                await _seatService.GenerateFlightSeatsAsync(schedule.ScheduleID);
                var reservedCount = await _context.FlightSeats
                    .Where(fs => fs.ScheduleId == schedule.ScheduleID && fs.Status == FlightSeatStatus.Reserved)
                    .CountAsync();
                var availableSeats = f.TotalSeats - reservedCount;
                if (availableSeats < passengers) continue;

                // Pricing
                var daysBeforeDeparture = (newDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
                var timingMultiplier = daysBeforeDeparture switch
                {
                    >= 30 => 0.80m,
                    >= 15 => 1.00m,
                    >= 7 => 1.20m,
                    _ => 1.50m
                };
                var classMultiplier = reservation.Class switch
                {
                    "Business" => 2.0m,
                    "First" => 3.5m,
                    _ => 1.0m
                };
                var basePrice = f.BaseFare;
                var finalPrice = basePrice * classMultiplier * timingMultiplier * passengers;

                results.Add(new ARS.ViewModels.FlightResultItem
                {
                    FlightID = f.FlightID,
                    FlightNumber = f.FlightNumber,
                    OriginCity = f.OriginCity?.CityName ?? "",
                    OriginAirportCode = f.OriginCity?.AirportCode ?? "",
                    DestinationCity = f.DestinationCity?.CityName ?? "",
                    DestinationAirportCode = f.DestinationCity?.AirportCode ?? "",
                    DepartureTime = schedule.Date.ToDateTime(TimeOnly.FromDateTime(f.DepartureTime)),
                    ArrivalTime = schedule.Date.ToDateTime(TimeOnly.FromDateTime(f.ArrivalTime)),
                    Duration = f.Duration,
                    AircraftType = f.AircraftType,
                    AvailableSeats = availableSeats,
                    BasePrice = basePrice,
                    FinalPrice = finalPrice,
                    ScheduleID = schedule.ScheduleID
                });
            }

            // Pagination
            const int pageSize = 5;
            var totalFlights = results.Count;
            var totalPages = (int)Math.Ceiling(totalFlights / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));
            
            var pagedResults = results
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var vm = new ARS.ViewModels.RescheduleSearchResultViewModel
            {
                Reservation = reservation,
                NewDate = newDate,
                Passengers = passengers,
                Class = reservation.Class,
                Flights = pagedResults,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize
            };

            return View("RescheduleResults", vm);
        }

        // GET: Reservation/RescheduleSelectSeats?reservationId=1&flightId=2&scheduleId=3&newDate=2025-11-16
        public async Task<IActionResult> RescheduleSelectSeats(int reservationId, int flightId, int scheduleId, DateOnly newDate)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var reservation = await _context.Reservations
                .Include(r => r.Payments)
                .Include(r => r.Flight)
                    .ThenInclude(f => f.OriginCity)
                .Include(r => r.Flight)
                    .ThenInclude(f => f.DestinationCity)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

            if (reservation == null) return NotFound();
            
            // Allow access if user owns reservation OR user is admin
            var isAdmin = User.IsInRole("Admin");
            if (reservation.UserID != currentUser.Id && !isAdmin) return Forbid();

            var flight = await _context.Flights
                .Include(f => f.OriginCity)
                .Include(f => f.DestinationCity)
                .Include(f => f.SeatLayout)
                .FirstOrDefaultAsync(f => f.FlightID == flightId);
            
            if (flight == null) return NotFound();

            // Ensure FlightSeats are generated for this schedule
            await _seatService.GenerateFlightSeatsAsync(scheduleId);

            // compute new total price
            var passengers = reservation.NumAdults + reservation.NumChildren + reservation.NumSeniors;
            var daysBefore = (newDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
            var timingMultiplier = daysBefore switch
            {
                >= 30 => 0.80m,
                >= 15 => 1.00m,
                >= 7 => 1.20m,
                _ => 1.50m
            };
            var classMultiplier = reservation.Class switch
            {
                "Business" => 2.0m,
                "First" => 3.5m,
                _ => 1.0m
            };
            var newTotal = Math.Round(flight.BaseFare * classMultiplier * timingMultiplier * passengers, 2);

            var totalPaid = (reservation.Payments ?? Enumerable.Empty<Payment>()).Where(p => p.TransactionStatus == "Completed").Sum(p => p.Amount);
            var difference = Math.Round(newTotal - totalPaid, 2);

            ViewBag.ReservationID = reservationId;
            ViewBag.FlightID = flightId;
            ViewBag.ScheduleID = scheduleId;
            ViewBag.NewDate = newDate;
            ViewBag.Flight = flight;
            ViewBag.Reservation = reservation;
            ViewBag.Passengers = passengers;
            ViewBag.NewTotal = newTotal;
            ViewBag.TotalPaid = totalPaid;
            ViewBag.Difference = difference;
            ViewBag.SeatLayoutId = flight.SeatLayoutId;

            return View();
        }

        // GET: Reservation/ConfirmReschedule?reservationId=1&flightId=2&scheduleId=3&newDate=2025-11-16&selectedSeats=1A,1B
        public async Task<IActionResult> ConfirmReschedule(int reservationId, int flightId, int scheduleId, DateOnly newDate, string? selectedSeats = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var reservation = await _context.Reservations
                .Include(r => r.Payments)
                .Include(r => r.Flight)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

            if (reservation == null) return NotFound();
            
            // Allow access if user owns reservation OR user is admin
            var isAdmin = User.IsInRole("Admin");
            if (reservation.UserID != currentUser.Id && !isAdmin) return Forbid();

            var flight = await _context.Flights
                .Include(f => f.OriginCity)
                .Include(f => f.DestinationCity)
                .FirstOrDefaultAsync(f => f.FlightID == flightId);
            
            if (flight == null) return NotFound();

            // compute new total price
            var passengers = reservation.NumAdults + reservation.NumChildren + reservation.NumSeniors;
            var daysBefore = (newDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
            var timingMultiplier = daysBefore switch
            {
                >= 30 => 0.80m,
                >= 15 => 1.00m,
                >= 7 => 1.20m,
                _ => 1.50m
            };
            var classMultiplier = reservation.Class switch
            {
                "Business" => 2.0m,
                "First" => 3.5m,
                _ => 1.0m
            };
            var newTotal = Math.Round(flight.BaseFare * classMultiplier * timingMultiplier * passengers, 2);

            var totalPaid = (reservation.Payments ?? Enumerable.Empty<Payment>()).Where(p => p.TransactionStatus == "Completed").Sum(p => p.Amount);
            var difference = Math.Round(newTotal - totalPaid, 2);

            var vm = new ARS.ViewModels.ConfirmRescheduleViewModel
            {
                Reservation = reservation,
                NewFlight = flight,
                NewScheduleID = scheduleId,
                NewDate = newDate,
                NewTotal = newTotal,
                TotalPaid = totalPaid,
                Difference = difference,
                SelectedSeats = selectedSeats ?? ""
            };

            return View(vm);
        }

        // POST: Reservation/ConfirmReschedule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReschedulePost(int reservationId, int flightId, int scheduleId, DateOnly newDate, string? selectedSeats = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var oldReservation = await _context.Reservations
                .Include(r => r.Payments)
                .Include(r => r.FlightSeat)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

            if (oldReservation == null) return NotFound();
            
            // Allow access if user owns reservation OR user is admin
            var isAdmin = User.IsInRole("Admin");
            if (oldReservation.UserID != currentUser.Id && !isAdmin) return Forbid();

            var flight = await _context.Flights.FindAsync(flightId);
            if (flight == null) return NotFound();

            // Cancel old reservation seats - make them available for others
            await _seatService.CancelReservationSeatAsync(reservationId);

            var passengers = oldReservation.NumAdults + oldReservation.NumChildren + oldReservation.NumSeniors;
            
            // Parse seat assignments JSON (new multi-seat system)
            List<(int flightSeatId, string seatLabel, string? passengerType)> parsedSeatAssignments = new();
            try
            {
                var seatJson = Request.Form["SeatAssignmentsJson"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(seatJson))
                {
                    using var doc = JsonDocument.Parse(seatJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("seats", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in seatsEl.EnumerateArray())
                            {
                                var fsId = item.TryGetProperty("flightSeatId", out var idEl) && idEl.TryGetInt32(out var idVal) ? idVal : 0;
                                var seatLabel = item.TryGetProperty("seatLabel", out var slEl) ? slEl.GetString() ?? string.Empty : string.Empty;
                                var pType = item.TryGetProperty("passengerType", out var ptEl) ? ptEl.GetString() : null;
                                if (fsId > 0 && !string.IsNullOrWhiteSpace(seatLabel))
                                {
                                    parsedSeatAssignments.Add((fsId, seatLabel.Trim(), pType));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore JSON parse errors; fall back to legacy comma-separated path
            }
            
            // Calculate total paid from old reservation
            var totalPaid = (oldReservation.Payments ?? Enumerable.Empty<Payment>())
                .Where(p => p.TransactionStatus == "Completed")
                .Sum(p => p.Amount);

            // Create new reservation
            var newReservation = new Reservation
            {
                UserID = oldReservation.UserID,
                FlightID = flightId,
                ScheduleID = scheduleId,
                TravelDate = newDate,
                NumAdults = oldReservation.NumAdults,
                NumChildren = oldReservation.NumChildren,
                NumSeniors = oldReservation.NumSeniors,
                Class = oldReservation.Class,
                BookingDate = DateOnly.FromDateTime(DateTime.Now),
                ConfirmationNumber = GenerateConfirmationNumber(),
                BlockingNumber = GenerateBlockingNumber(),
                Status = "Confirmed"
            };

            _context.Reservations.Add(newReservation);
            await _context.SaveChangesAsync(); // Save to get the new ReservationID
            
            // Reserve new seats and calculate pricing
            var newSeatLabels = new List<string>();
            decimal newTotal = 0;
            var daysBefore = (newDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
            var timingMultiplier = daysBefore switch
            {
                >= 30 => 0.80m,
                >= 15 => 1.00m,
                >= 7 => 1.20m,
                _ => 1.50m
            };
            
            if (parsedSeatAssignments.Any())
            {
                // New multi-seat system with per-seat pricing
                var a = 0; var c = 0; var s = 0;
                var highestClass = CabinClass.Economy;
                var legsToCreate = new List<ReservationLeg>();
                
                foreach (var (fsId, seatLabel, passengerType) in parsedSeatAssignments)
                {
                    // Count passengers by type
                    if (string.Equals(passengerType, "child", StringComparison.OrdinalIgnoreCase)) c++;
                    else if (string.Equals(passengerType, "senior", StringComparison.OrdinalIgnoreCase)) s++;
                    else a++;
                    
                    // Get seat details for pricing
                    var fs = await _context.FlightSeats.Include(x => x.AircraftSeat).FirstOrDefaultAsync(x => x.FlightSeatId == fsId);
                    if (fs?.AircraftSeat == null)
                    {
                        _context.Reservations.Remove(newReservation);
                        await _context.SaveChangesAsync();
                        TempData["ErrorMessage"] = $"Seat {seatLabel} is no longer available.";
                        return RedirectToAction("RescheduleSelectSeats", new { reservationId, flightId, scheduleId, newDate });
                    }
                    
                    // Check availability
                    if (fs.Status == FlightSeatStatus.Reserved)
                    {
                        _context.Reservations.Remove(newReservation);
                        await _context.SaveChangesAsync();
                        TempData["ErrorMessage"] = $"Seat {seatLabel} has just been booked by someone else.";
                        return RedirectToAction("RescheduleSelectSeats", new { reservationId, flightId, scheduleId, newDate });
                    }
                    
                    // Calculate price for this seat
                    var seatClassMultiplier = fs.AircraftSeat.CabinClass switch
                    {
                        CabinClass.Business => 2.0m,
                        CabinClass.First => 3.5m,
                        _ => 1.0m
                    };
                    newTotal += flight.BaseFare * seatClassMultiplier * timingMultiplier;
                    
                    // Track highest class
                    if (fs.AircraftSeat.CabinClass < highestClass) highestClass = fs.AircraftSeat.CabinClass;
                    
                    newSeatLabels.Add(seatLabel);
                    
                    // Create ReservationLeg for this seat
                    Seat? resSeat = null;
                    if (flight.SeatLayoutId != null)
                    {
                        resSeat = await _context.Seats
                            .FirstOrDefaultAsync(s => s.SeatLayoutId == flight.SeatLayoutId && s.Label == seatLabel);
                    }
                    
                    var leg = new ReservationLeg
                    {
                        ReservationID = newReservation.ReservationID,
                        FlightID = flightId,
                        ScheduleID = scheduleId,
                        TravelDate = newDate,
                        SeatLabel = seatLabel,
                        SeatId = resSeat?.SeatId,
                        FlightSeatId = fsId,
                        LegOrder = 1
                    };
                    legsToCreate.Add(leg);
                }
                
                // Update reservation with actual passenger counts and class
                newReservation.NumAdults = a;
                newReservation.NumChildren = c;
                newReservation.NumSeniors = s;
                newReservation.Class = highestClass switch
                {
                    CabinClass.First => "First",
                    CabinClass.Business => "Business",
                    _ => "Economy"
                };
                
                if (newSeatLabels.Any())
                {
                    newReservation.SeatLabel = string.Join(", ", newSeatLabels);
                    newReservation.FlightSeatId = parsedSeatAssignments.First().flightSeatId;
                }
                
                // Save legs
                _context.ReservationLegs.AddRange(legsToCreate);
                await _context.SaveChangesAsync();
                
                // Reserve all FlightSeats
                foreach (var leg in legsToCreate)
                {
                    if (leg.FlightSeatId.HasValue)
                    {
                        var reserveResult = await _seatService.ReserveSeatForLegAsync(leg.FlightSeatId.Value, leg.ReservationLegID);
                        if (!reserveResult)
                        {
                            _context.Reservations.Remove(newReservation);
                            await _context.SaveChangesAsync();
                            TempData["ErrorMessage"] = $"Failed to reserve seat {leg.SeatLabel}.";
                            return RedirectToAction("RescheduleSelectSeats", new { reservationId, flightId, scheduleId, newDate });
                        }
                    }
                }
                
                newTotal = Math.Round(newTotal, 2);
            }
            else if (!string.IsNullOrEmpty(selectedSeats))
            {
                // Legacy comma-separated seat selection path
                // Legacy comma-separated seat selection path
                var seatLabelArray = selectedSeats.Split(',', StringSplitOptions.RemoveEmptyEntries);
                
                if (seatLabelArray.Length != passengers)
                {
                    _context.Reservations.Remove(newReservation);
                    await _context.SaveChangesAsync();
                    TempData["ErrorMessage"] = $"Please select exactly {passengers} seat(s).";
                    return RedirectToAction("RescheduleSelectSeats", new { reservationId, flightId, scheduleId, newDate });
                }

                // Get all available seats for this schedule
                var availableSeats = await _seatService.GetAvailableSeatsAsync(scheduleId);
                
                // Reserve each selected seat and calculate price based on seat classes
                decimal seatClassBasedPrice = 0;
                foreach (var label in seatLabelArray)
                {
                    var seatToReserve = availableSeats.FirstOrDefault(s => s.Label == label.Trim());
                    if (seatToReserve == null || !seatToReserve.IsAvailable)
                    {
                        _context.Reservations.Remove(newReservation);
                        await _context.SaveChangesAsync();
                        TempData["ErrorMessage"] = $"Seat {label} is no longer available.";
                        return RedirectToAction("RescheduleSelectSeats", new { reservationId, flightId, scheduleId, newDate });
                    }

                    var reserveResult = await _seatService.ReserveSeatAsync(seatToReserve.FlightSeatId, newReservation.ReservationID);
                    if (!reserveResult)
                    {
                        _context.Reservations.Remove(newReservation);
                        await _context.SaveChangesAsync();
                        TempData["ErrorMessage"] = $"Failed to reserve seat {label}.";
                        return RedirectToAction("RescheduleSelectSeats", new { reservationId, flightId, scheduleId, newDate });
                    }
                    
                    var seatClassMultiplier = seatToReserve.CabinClass switch
                    {
                        CabinClass.Business => 2.0m,
                        CabinClass.First => 3.5m,
                        _ => 1.0m
                    };
                    
                    seatClassBasedPrice += flight.BaseFare * seatClassMultiplier * timingMultiplier;
                    newSeatLabels.Add(label.Trim());
                }

                // Link first seat to reservation
                if (newSeatLabels.Any())
                {
                    var firstSeatInfo = availableSeats.FirstOrDefault(s => s.Label == newSeatLabels[0]);
                    if (firstSeatInfo != null)
                    {
                        newReservation.FlightSeatId = firstSeatInfo.FlightSeatId;
                        newReservation.SeatLabel = string.Join(", ", newSeatLabels);
                        
                        // Update reservation class based on highest class selected
                        var highestClass = availableSeats
                            .Where(s => newSeatLabels.Contains(s.Label))
                            .Max(s => s.CabinClass);
                        
                        newReservation.Class = highestClass switch
                        {
                            CabinClass.First => "First",
                            CabinClass.Business => "Business",
                            _ => "Economy"
                        };
                    }
                }
                
                // Use seat-based pricing
                newTotal = Math.Round(seatClassBasedPrice, 2);
            }
            else
            {
                // No seats selected, use original class pricing
                var classMultiplier = newReservation.Class switch
                {
                    "Business" => 2.0m,
                    "First" => 3.5m,
                    _ => 1.0m
                };
                newTotal = Math.Round(flight.BaseFare * classMultiplier * timingMultiplier * passengers, 2);
            }

            var difference = Math.Round(newTotal - totalPaid, 2);

            // Transfer the old payments to new reservation as "Applied from previous booking"
            if (totalPaid > 0)
            {
                var transferPayment = new Payment
                {
                    ReservationID = newReservation.ReservationID,
                    Amount = totalPaid,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "TransferFromReschedule",
                    TransactionStatus = "Completed",
                    TransactionRefNo = $"TRANSFER-{oldReservation.ConfirmationNumber}"
                };
                _context.Payments.Add(transferPayment);
            }

            // If difference > 0, create a pending payment record for the balance due
            if (difference > 0)
            {
                var payment = new Payment
                {
                    ReservationID = newReservation.ReservationID,
                    Amount = difference,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "RescheduleDue",
                    TransactionStatus = "Pending",
                    TransactionRefNo = null
                };
                _context.Payments.Add(payment);
                newReservation.Status = "Pending"; // Set to Pending if payment is due
            }
            else if (difference < 0)
            {
                // process refund for the overpaid amount
                var refundAmount = Math.Abs(difference);
                var refundPercent = totalPaid > 0 ? Math.Round((refundAmount / totalPaid) * 100m, 2) : 0m;
                var refund = new Refund
                {
                    ReservationID = newReservation.ReservationID,
                    RefundAmount = refundAmount,
                    RefundDate = DateTime.Now,
                    RefundPercentage = refundPercent
                };
                _context.Refunds.Add(refund);
            }

            // Cancel the old reservation
            oldReservation.Status = "Cancelled";

            await _context.SaveChangesAsync();

            var seatInfo = newSeatLabels.Any() ? $" Seats: {string.Join(", ", newSeatLabels)}." : "";
            TempData["SuccessMessage"] = difference > 0
                ? $"Reservation rescheduled successfully. New confirmation: {newReservation.ConfirmationNumber}.{seatInfo} An additional payment of ${difference:N2} is required."
                : difference < 0
                    ? $"Reservation rescheduled successfully. New confirmation: {newReservation.ConfirmationNumber}.{seatInfo} A refund of ${Math.Abs(difference):N2} will be processed."
                    : $"Reservation rescheduled successfully. New confirmation: {newReservation.ConfirmationNumber}.{seatInfo}";

            return RedirectToAction("Details", new { id = newReservation.ReservationID });
        }

        // POST: Reservation/CancelReschedule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelReschedule(int reservationId, int? scheduleId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

            if (reservation == null)
            {
                return NotFound();
            }

            // If reservation is in "Rescheduled" status, we need to revert it
            if (reservation.Status == "Rescheduled")
            {
                // Remove the pending reschedule payment
                var reschedulePendingPayment = reservation.Payments?
                    .FirstOrDefault(p => p.PaymentMethod == "RescheduleDue" && p.TransactionStatus == "Pending");
                
                if (reschedulePendingPayment != null)
                {
                    _context.Payments.Remove(reschedulePendingPayment);
                }

                // Release the newly reserved seats
                await _seatService.CancelReservationSeatAsync(reservationId);

                // Note: We cannot easily revert to the original flight/schedule/date without storing that data
                // For now, we'll just change the status back to Confirmed
                reservation.Status = "Confirmed";
                
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Reschedule has been cancelled. The pending payment has been removed. Please contact support if you need to revert to your original flight details.";
            }
            else
            {
                // Release any reserved seats for this reschedule attempt (in-progress reschedule)
                if (scheduleId.HasValue)
                {
                    await _seatService.CancelReservationSeatAsync(reservationId);
                }

                TempData["InfoMessage"] = "Reschedule cancelled. No changes were made to your reservation.";
            }
            
            return RedirectToAction("Details", new { id = reservationId });
        }

        private string GenerateConfirmationNumber()
        {
            return $"ARS{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
        }

        private string GenerateBlockingNumber()
        {
            return $"BLK{DateTime.Now:yyyyMMddHHmmss}";
        }

        private string BuildBookingConfirmationEmail(Reservation reservation, User user, List<ReservationLeg> legs, decimal totalAmount)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Dear {user.FirstName} {user.LastName},");
            sb.AppendLine();
            sb.AppendLine("Thank you for booking with ARS Airlines! Your reservation has been created.");
            sb.AppendLine();
            sb.AppendLine("RESERVATION DETAILS:");
            sb.AppendLine($"Confirmation Number: {reservation.ConfirmationNumber}");
            sb.AppendLine($"Booking Number: {reservation.BlockingNumber}");
            sb.AppendLine($"Reservation Status: {reservation.Status}");
            sb.AppendLine($"Passengers: {reservation.NumAdults} Adult(s), {reservation.NumChildren} Child(ren), {reservation.NumSeniors} Senior(s)");
            sb.AppendLine($"Class: {reservation.Class}");
            sb.AppendLine();
            sb.AppendLine("FLIGHT ITINERARY (Multi-Leg Journey):");
            
            int legNum = 1;
            foreach (var leg in legs.OrderBy(l => l.TravelDate))
            {
                sb.AppendLine($"\nLeg {legNum}:");
                sb.AppendLine($"  Flight: {leg.Flight?.FlightNumber}");
                sb.AppendLine($"  Route: {leg.Flight?.OriginCity?.CityName} → {leg.Flight?.DestinationCity?.CityName}");
                sb.AppendLine($"  Date: {leg.TravelDate:yyyy-MM-dd}");
                sb.AppendLine($"  Departure: {leg.Flight?.DepartureTime:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"  Arrival: {leg.Flight?.ArrivalTime:yyyy-MM-dd HH:mm}");
                if (!string.IsNullOrEmpty(leg.SeatLabel))
                {
                    sb.AppendLine($"  Seat: {leg.SeatLabel}");
                }
                legNum++;
            }
            
            sb.AppendLine();
            sb.AppendLine($"TOTAL AMOUNT: ${totalAmount:F2}");
            sb.AppendLine();
            sb.AppendLine("NEXT STEPS:");
            sb.AppendLine("1. Complete your payment to confirm your booking");
            sb.AppendLine("2. You will receive a payment confirmation email once payment is completed");
            sb.AppendLine("3. Please arrive at the airport at least 2 hours before departure");
            sb.AppendLine();
            sb.AppendLine("You can view and manage your reservation by logging into your account.");
            sb.AppendLine();
            sb.AppendLine("Thank you for choosing ARS Airlines!");
            sb.AppendLine();
            sb.AppendLine("Best regards,");
            sb.AppendLine("ARS Airlines Team");
            
            return sb.ToString();
        }

        private string BuildBookingConfirmationEmailSingle(Reservation reservation, User user, Flight flight, decimal totalAmount)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Dear {user.FirstName} {user.LastName},");
            sb.AppendLine();
            sb.AppendLine("Thank you for booking with ARS Airlines! Your reservation has been created.");
            sb.AppendLine();
            sb.AppendLine("RESERVATION DETAILS:");
            sb.AppendLine($"Confirmation Number: {reservation.ConfirmationNumber}");
            sb.AppendLine($"Booking Number: {reservation.BlockingNumber}");
            sb.AppendLine($"Reservation Status: {reservation.Status}");
            sb.AppendLine($"Passengers: {reservation.NumAdults} Adult(s), {reservation.NumChildren} Child(ren), {reservation.NumSeniors} Senior(s)");
            sb.AppendLine($"Class: {reservation.Class}");
            sb.AppendLine();
            sb.AppendLine("FLIGHT DETAILS:");
            sb.AppendLine($"Flight Number: {flight.FlightNumber}");
            sb.AppendLine($"Route: {flight.OriginCity?.CityName} → {flight.DestinationCity?.CityName}");
            sb.AppendLine($"Travel Date: {reservation.TravelDate:yyyy-MM-dd}");
            sb.AppendLine($"Departure: {flight.DepartureTime:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Arrival: {flight.ArrivalTime:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            // Person-to-seat assignment for single one-way bookings
            var assignedSeats = new List<string>();
            if (reservation.Legs != null && reservation.Legs.Any())
            {
                // Use legs for seat labels (single-leg bookings will have LegOrder=1 entries)
                foreach (var leg in reservation.Legs.Where(l => l.LegOrder == 1).OrderBy(l => l.ReservationLegID))
                {
                    if (!string.IsNullOrWhiteSpace(leg.SeatLabel))
                        assignedSeats.Add(leg.SeatLabel!);
                }
            }
            else if (!string.IsNullOrEmpty(reservation.SeatLabel))
            {
                // Legacy path: comma-separated labels
                assignedSeats.AddRange(reservation.SeatLabel.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            if (assignedSeats.Any())
            {
                sb.AppendLine("SEAT ASSIGNMENTS:");
                // Build person descriptors: Adults, then Children, then Seniors
                var persons = new List<string>();
                for (int i = 1; i <= reservation.NumAdults; i++) persons.Add($"Adult {i}");
                for (int i = 1; i <= reservation.NumChildren; i++) persons.Add($"Child {i}");
                for (int i = 1; i <= reservation.NumSeniors; i++) persons.Add($"Senior {i}");
                // Zip persons to seats by order
                for (int i = 0; i < Math.Min(persons.Count, assignedSeats.Count); i++)
                {
                    sb.AppendLine($"  {persons[i]} → Seat {assignedSeats[i]}");
                }
                // If extra seats exist (shouldn’t), list them
                for (int i = persons.Count; i < assignedSeats.Count; i++)
                {
                    sb.AppendLine($"  Seat {assignedSeats[i]}");
                }
            }
            sb.AppendLine();
            sb.AppendLine($"TOTAL AMOUNT: ${totalAmount:F2}");
            sb.AppendLine();
            sb.AppendLine("NEXT STEPS:");
            sb.AppendLine("1. Complete your payment to confirm your booking");
            sb.AppendLine("2. You will receive a payment confirmation email once payment is completed");
            sb.AppendLine("3. Please arrive at the airport at least 2 hours before departure");
            sb.AppendLine();
            sb.AppendLine("You can view and manage your reservation by logging into your account.");
            sb.AppendLine();
            sb.AppendLine("Thank you for choosing ARS Airlines!");
            sb.AppendLine();
            sb.AppendLine("Best regards,");
            sb.AppendLine("ARS Airlines Team");
            
            return sb.ToString();
        }
    }
}
