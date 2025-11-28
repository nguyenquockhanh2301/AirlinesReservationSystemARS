using ARS.Models;

namespace ARS.ViewModels
{
    public class ConfirmRescheduleViewModel
    {
        public Reservation Reservation { get; set; } = new Reservation();
        public Flight? NewFlight { get; set; }
        public int NewScheduleID { get; set; }
        public DateOnly NewDate { get; set; }
        public decimal NewTotal { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Difference { get; set; }
        public string SelectedSeats { get; set; } = string.Empty;
    }
}
