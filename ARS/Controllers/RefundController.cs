using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ARS.Data;
using ARS.Models;
using ARS.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace ARS.Controllers
{
    public class RefundController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ARS.Services.ISeatService _seatService;

        public RefundController(ApplicationDbContext context, UserManager<User> userManager, ARS.Services.ISeatService seatService)
        {
            _context = context;
            _userManager = userManager;
            _seatService = seatService;
        }

        // GET: Refund/Cancel?reservationId=5
        public async Task<IActionResult> Cancel(int reservationId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "Please login to cancel a reservation.";
                return RedirectToAction("Login", "Account", new { returnUrl = $"/Reservation/Details/{reservationId}" });
            }

            var reservation = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Flight)
                    .ThenInclude(f => f.OriginCity)
                .Include(r => r.Flight)
                    .ThenInclude(f => f.DestinationCity)
                .Include(r => r.Payments)
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

            if (reservation.Status == "Cancelled")
            {
                TempData["InfoMessage"] = "This reservation is already cancelled.";
                return RedirectToAction("Details", "Reservation", new { id = reservationId });
            }

            // Calculate potential refund based on days before departure
            var daysBeforeDeparture = (reservation.TravelDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;

            decimal refundRate = daysBeforeDeparture switch
            {
                >= 30 => 1.00m,
                >= 15 => 0.80m,
                >= 7 => 0.50m,
                _ => 0.00m
            };

            var totalPaid = reservation.Payments?.Where(p => p.TransactionStatus == "Completed").Sum(p => p.Amount) ?? 0m;
            var potentialRefundAmount = Math.Round(totalPaid * refundRate, 2);

            var vm = new RefundViewModel
            {
                Reservation = reservation,
                DaysBeforeDeparture = daysBeforeDeparture,
                PotentialRefundAmount = potentialRefundAmount,
                RefundPercentage = refundRate * 100m
            };

            return View(vm);
        }

        // POST: Refund/ConfirmCancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCancel(int reservationId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "Please login to cancel a reservation.";
                return RedirectToAction("Login", "Account");
            }

            var reservation = await _context.Reservations
                .Include(r => r.Payments)
                .Include(r => r.Legs)
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

            if (reservation.Status == "Cancelled")
            {
                TempData["InfoMessage"] = "This reservation is already cancelled.";
                return RedirectToAction("Details", "Reservation", new { id = reservationId });
            }

            var daysBeforeDeparture = (reservation.TravelDate.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days;

            decimal refundRate = daysBeforeDeparture switch
            {
                >= 30 => 1.00m,
                >= 15 => 0.80m,
                >= 7 => 0.50m,
                _ => 0.00m
            };

            var totalPaid = reservation.Payments?.Where(p => p.TransactionStatus == "Completed").Sum(p => p.Amount) ?? 0m;
            var refundAmount = Math.Round(totalPaid * refundRate, 2);

            // Create Refund record
            var refund = new Refund
            {
                ReservationID = reservation.ReservationID,
                RefundAmount = refundAmount,
                RefundDate = DateTime.Now,
                RefundPercentage = refundRate * 100m
            };

            reservation.Status = "Cancelled";

            // Release the seats using SeatService - for single-flight reservation
            if (reservation.FlightSeatId.HasValue)
            {
                await _seatService.CancelReservationSeatAsync(reservation.ReservationID);
            }

            // Release seats for multi-leg reservations
            if (reservation.Legs != null && reservation.Legs.Any())
            {
                foreach (var leg in reservation.Legs)
                {
                    if (leg.FlightSeatId.HasValue)
                    {
                        await _seatService.CancelReservationSeatForLegAsync(leg.ReservationLegID);
                    }
                }
            }

            // Mark payments as refunded where applicable
            if (refundAmount > 0)
            {
                foreach (var payment in reservation.Payments.Where(p => p.TransactionStatus == "Completed"))
                {
                    payment.TransactionStatus = "Refunded";
                }
            }

            _context.Refunds.Add(refund);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = refundAmount > 0 ? $"Reservation cancelled. A refund of ${refundAmount:N2} has been processed." : "Reservation cancelled. No refund is due based on the cancellation policy.";

            return RedirectToAction("Details", "Reservation", new { id = reservationId });
        }
    }
}
