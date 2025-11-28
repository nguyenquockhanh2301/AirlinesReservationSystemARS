using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ARS.Data;
using ARS.Models;
using Microsoft.AspNetCore.Identity;
using ARS.ViewModels;

namespace ARS.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ARS.Services.IEmailService _emailService;

        public PaymentController(ApplicationDbContext context, UserManager<User> userManager, IConfiguration configuration, ARS.Services.IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _emailService = emailService;
        }

        // GET: Payment/Index?reservationId=7
        public async Task<IActionResult> Index(int reservationId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "Please login to make a payment.";
                return RedirectToAction("Login", "Account", new { returnUrl = $"/Payment?reservationId={reservationId}" });
            }

            var reservation = await _context.Reservations
                .Include(r => r.Flight)
                    .ThenInclude(f => f!.OriginCity)
                .Include(r => r.Flight)
                    .ThenInclude(f => f!.DestinationCity)
                .Include(r => r.Payments)
                .Include(r => r.Schedule)
                .Include(r => r.Legs)
                    .ThenInclude(l => l.Flight)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

            if (reservation == null)
            {
                return NotFound();
            }

            // Allow access if user owns reservation OR user is admin
            var isAdmin = User.IsInRole("Admin");
            if (reservation.UserID != currentUser.Id && !isAdmin)
            {
                return Forbid();
            }

            // Calculate amounts
            var totalPaid = reservation.Payments?
                .Where(p => p.TransactionStatus == "Completed")
                .Sum(p => p.Amount) ?? 0;

            var pendingPayments = reservation.Payments?
                .Where(p => p.TransactionStatus == "Pending")
                .ToList() ?? new List<Payment>();

            var amountDue = pendingPayments.Sum(p => p.Amount);

            // If no pending payments exist, create one based on reservation details
            if (amountDue <= 0 && reservation.Status == "Pending")
            {
                // Calculate the price for this reservation
                var flight = reservation.Flight ?? await _context.Flights.FindAsync(reservation.FlightID);
                
                if (flight != null)
                {
                    var passengers = reservation.NumAdults + reservation.NumChildren + reservation.NumSeniors;
                    var travelDate = reservation.TravelDate;
                    var daysBefore = (travelDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
                    
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
                    
                    // For multi-leg reservations, calculate based on all legs
                    decimal totalPrice = 0;
                    if (reservation.Legs != null && reservation.Legs.Any())
                    {
                        foreach (var leg in reservation.Legs)
                        {
                            var legFlight = leg.Flight ?? await _context.Flights.FindAsync(leg.FlightID);
                            if (legFlight != null)
                            {
                                var legDaysBefore = (leg.TravelDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;
                                var legTimingMultiplier = legDaysBefore switch
                                {
                                    >= 30 => 0.80m,
                                    >= 15 => 1.00m,
                                    >= 7 => 1.20m,
                                    _ => 1.50m
                                };
                                totalPrice += legFlight.BaseFare * classMultiplier * legTimingMultiplier * passengers;
                            }
                        }
                    }
                    else
                    {
                        totalPrice = flight.BaseFare * classMultiplier * timingMultiplier * passengers;
                    }
                    
                    totalPrice = Math.Round(totalPrice, 2);
                    
                    // Create the initial payment record
                    var initialPayment = new Payment
                    {
                        ReservationID = reservation.ReservationID,
                        Amount = totalPrice,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = "Pending",
                        TransactionStatus = "Pending",
                        TransactionRefNo = null
                    };
                    
                    _context.Payments.Add(initialPayment);
                    await _context.SaveChangesAsync();
                    
                    amountDue = totalPrice;
                    pendingPayments = new List<Payment> { initialPayment };
                }
            }

            if (amountDue <= 0)
            {
                TempData["InfoMessage"] = "No pending payment for this reservation.";
                return RedirectToAction("Details", "Reservation", new { id = reservationId });
            }

            var viewModel = new PaymentViewModel
            {
                Reservation = reservation,
                TotalPaid = totalPaid,
                AmountDue = amountDue,
                PendingPayments = pendingPayments,
                PayPalClientId = _configuration["PayPal:ClientId"] ?? ""
            };

            return View(viewModel);
        }

        // POST: Payment/ProcessPayPal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayPal(int reservationId, string orderId, string payerId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var reservation = await _context.Reservations
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

            if (reservation == null)
            {
                return Json(new { success = false, message = "Reservation not found" });
            }

            // Verify user owns reservation
            if (reservation.UserID != currentUser.Id && !User.IsInRole("Admin"))
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            // Get pending payments
            var pendingPayments = reservation.Payments?
                .Where(p => p.TransactionStatus == "Pending")
                .ToList() ?? new List<Payment>();

            if (!pendingPayments.Any())
            {
                return Json(new { success = false, message = "No pending payments" });
            }

            // Update all pending payments to completed
            foreach (var payment in pendingPayments)
            {
                payment.TransactionStatus = "Completed";
                payment.PaymentMethod = "PayPal";
                payment.TransactionRefNo = orderId;
                payment.PaymentDate = DateTime.Now;
            }

            // Update reservation status to Confirmed if it was Pending
            if (reservation.Status == "Pending")
            {
                reservation.Status = "Confirmed";
            }

            await _context.SaveChangesAsync();

            // Send payment confirmation email
            try
            {
                Console.WriteLine($"[PAYMENT DEBUG] Attempting to send payment confirmation email...");
                var user = await _userManager.FindByIdAsync(reservation.UserID.ToString());
                if (user != null)
                {
                    Console.WriteLine($"[PAYMENT DEBUG] Found user {user.Email}");
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
                        .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

                    if (fullReservation != null)
                    {
                        Console.WriteLine($"[PAYMENT DEBUG] Found reservation {fullReservation.ReservationID}");
                        var totalAmount = pendingPayments.Sum(p => p.Amount);
                        Console.WriteLine($"[PAYMENT DEBUG] Building payment confirmation email, amount: ${totalAmount}");
                        var emailBody = BuildPaymentConfirmationEmail(fullReservation, user, totalAmount, orderId);
                        Console.WriteLine($"[PAYMENT DEBUG] Sending payment email to {user.Email}");
                        await _emailService.SendAsync(user.Email, "Payment Confirmation - ARS Airlines", emailBody);
                        Console.WriteLine($"[PAYMENT DEBUG] Email send method completed");
                    }
                    else
                    {
                        Console.WriteLine($"[PAYMENT DEBUG] Reservation not found");
                    }
                }
                else
                {
                    Console.WriteLine($"[PAYMENT DEBUG] User not found");
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the payment if email fails
                Console.WriteLine($"[PAYMENT ERROR] Failed to send payment confirmation email: {ex.Message}");
                Console.WriteLine($"[PAYMENT ERROR] Stack trace: {ex.StackTrace}");
            }

            return Json(new { 
                success = true, 
                message = "Payment processed successfully",
                redirectUrl = Url.Action("Details", "Reservation", new { id = reservationId })
            });
        }

        // POST: Payment/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int reservationId)
        {
            TempData["InfoMessage"] = "Payment was cancelled.";
            return RedirectToAction("Details", "Reservation", new { id = reservationId });
        }

        private string BuildPaymentConfirmationEmail(Reservation reservation, User user, decimal amount, string transactionId)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Dear {user.FirstName} {user.LastName},");
            sb.AppendLine();
            sb.AppendLine("Thank you for your payment! Your booking has been confirmed.");
            sb.AppendLine();
            sb.AppendLine("PAYMENT DETAILS:");
            sb.AppendLine($"Transaction ID: {transactionId}");
            sb.AppendLine($"Amount Paid: ${amount:F2}");
            sb.AppendLine($"Payment Date: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Payment Method: PayPal");
            sb.AppendLine();
            sb.AppendLine("RESERVATION DETAILS:");
            sb.AppendLine($"Confirmation Number: {reservation.ConfirmationNumber}");
            sb.AppendLine($"Reservation Status: {reservation.Status}");
            sb.AppendLine($"Passengers: {reservation.NumAdults} Adult(s), {reservation.NumChildren} Child(ren), {reservation.NumSeniors} Senior(s)");
            sb.AppendLine($"Class: {reservation.Class}");
            
            if (reservation.Legs != null && reservation.Legs.Any())
            {
                sb.AppendLine();
                sb.AppendLine("FLIGHT ITINERARY (Multi-Leg Journey):");
                int legNum = 1;
                foreach (var leg in reservation.Legs.OrderBy(l => l.TravelDate))
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
            }
            else if (reservation.Flight != null)
            {
                sb.AppendLine();
                sb.AppendLine("FLIGHT DETAILS:");
                sb.AppendLine($"Flight Number: {reservation.Flight.FlightNumber}");
                sb.AppendLine($"Route: {reservation.Flight.OriginCity?.CityName} → {reservation.Flight.DestinationCity?.CityName}");
                sb.AppendLine($"Travel Date: {reservation.TravelDate:yyyy-MM-dd}");
                sb.AppendLine($"Departure: {reservation.Flight.DepartureTime:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"Arrival: {reservation.Flight.ArrivalTime:yyyy-MM-dd HH:mm}");
                if (!string.IsNullOrEmpty(reservation.SeatLabel))
                {
                    sb.AppendLine($"Seat: {reservation.SeatLabel}");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("Please arrive at the airport at least 2 hours before departure.");
            sb.AppendLine();
            sb.AppendLine("Thank you for choosing ARS Airlines!");
            sb.AppendLine();
            sb.AppendLine("Best regards,");
            sb.AppendLine("ARS Airlines Team");
            
            return sb.ToString();
        }
    }
}
