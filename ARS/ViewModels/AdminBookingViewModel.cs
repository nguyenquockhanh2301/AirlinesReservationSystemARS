using ARS.Models;

namespace ARS.ViewModels
{
    public class AdminBookingViewModel
    {
        public int ReservationID { get; set; }
        public string ConfirmationNumber { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string TravelDate { get; set; } = string.Empty;
        public int NumberOfSeats { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public string BookingDate { get; set; } = string.Empty;
        public bool IsMultiLeg { get; set; }
        public List<LegInfo> Legs { get; set; } = new List<LegInfo>();
    }

    public class LegInfo
    {
        public string Route { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
    }

    public class UserBookingsViewModel
    {
        public User User { get; set; } = null!;
        public List<AdminBookingViewModel> Bookings { get; set; } = new List<AdminBookingViewModel>();
    }
}
