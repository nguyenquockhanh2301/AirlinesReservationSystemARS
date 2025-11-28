using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ARS.Data;
using ARS.Models;
using ARS.Services;
using ARS.ViewModels;

namespace ARS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISeatService _seatService;

        public AdminController(ApplicationDbContext context, ISeatService seatService)
        {
            _context = context;
            _seatService = seatService;
        }

        // GET: Admin/UserManagement
        public IActionResult UserManagement()
        {
            return View();
        }

        // POST: Admin/SearchUser
        [HttpPost]
        public async Task<IActionResult> SearchUser(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["ErrorMessage"] = "Please enter an email address.";
                return RedirectToAction(nameof(UserManagement));
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email.Trim());

            if (user == null)
            {
                TempData["ErrorMessage"] = $"No user found with email: {email}";
                return RedirectToAction(nameof(UserManagement));
            }

            return RedirectToAction(nameof(UserBookings), new { userId = user.Id });
        }

        // GET: Admin/UserBookings/5
        public async Task<IActionResult> UserBookings(int userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(UserManagement));
            }

            var reservations = await _context.Reservations
                .Include(r => r.Flight)
                    .ThenInclude(f => f!.OriginCity)
                .Include(r => r.Flight)
                    .ThenInclude(f => f!.DestinationCity)
                .Include(r => r.Legs)
                    .ThenInclude(rl => rl.Flight)
                        .ThenInclude(f => f!.OriginCity)
                .Include(r => r.Legs)
                    .ThenInclude(rl => rl.Flight)
                        .ThenInclude(f => f!.DestinationCity)
                .Include(r => r.Schedule)
                .Include(r => r.Payments)
                .Where(r => r.UserID == userId)
                .OrderByDescending(r => r.BookingDate)
                .ToListAsync();

            // Transform to ViewModel with formatted dates
            var viewModel = new UserBookingsViewModel
            {
                User = user,
                Bookings = reservations.Select(r => new AdminBookingViewModel
                {
                    ReservationID = r.ReservationID,
                    ConfirmationNumber = r.ConfirmationNumber,
                    Route = r.Legs != null && r.Legs.Any()
                        ? string.Join(" → ", r.Legs.OrderBy(l => l.LegOrder).Select(l => $"{l.Flight?.OriginCity?.CityName} to {l.Flight?.DestinationCity?.CityName}"))
                        : $"{r.Flight?.OriginCity?.CityName} to {r.Flight?.DestinationCity?.CityName}",
                    TravelDate = r.TravelDate.ToDateTime(TimeOnly.MinValue).ToString("MMM dd, yyyy"),
                    NumberOfSeats = r.NumAdults + r.NumChildren + r.NumSeniors,
                    TotalPrice = r.Payments?.Sum(p => p.Amount) ?? 0,
                    Status = r.Status,
                    BookingDate = r.BookingDate.ToDateTime(TimeOnly.MinValue).ToString("MMM dd, yyyy"),
                    IsMultiLeg = r.Legs != null && r.Legs.Any(),
                    Legs = r.Legs?.OrderBy(l => l.LegOrder).Select(l => new LegInfo
                    {
                        Route = $"{l.Flight?.OriginCity?.CityName} to {l.Flight?.DestinationCity?.CityName}",
                        Date = l.TravelDate.ToDateTime(TimeOnly.MinValue).ToString("MMM dd, yyyy")
                    }).ToList() ?? new List<LegInfo>()
                }).ToList()
            };

            return View(viewModel);
        }

        // POST: Admin/CancelBooking/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int id, int userId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Legs)
                .FirstOrDefaultAsync(r => r.ReservationID == id);

            if (reservation == null)
            {
                TempData["ErrorMessage"] = "Reservation not found.";
                return RedirectToAction(nameof(UserBookings), new { userId });
            }

            if (reservation.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "This reservation is already cancelled.";
                return RedirectToAction(nameof(UserBookings), new { userId });
            }

            try
            {
                // Release seats
                if (reservation.Legs != null && reservation.Legs.Any())
                {
                    // Multi-leg reservation
                    foreach (var leg in reservation.Legs)
                    {
                        await _seatService.CancelReservationSeatForLegAsync(leg.ReservationLegID);
                    }
                }
                else
                {
                    // Single-flight reservation
                    await _seatService.CancelReservationSeatAsync(reservation.ReservationID);
                }

                // Update reservation status
                reservation.Status = "Cancelled";
                _context.Update(reservation);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Reservation {reservation.ConfirmationNumber} has been cancelled successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error cancelling reservation: {ex.Message}";
            }

            return RedirectToAction(nameof(UserBookings), new { userId });
        }
    }
}
