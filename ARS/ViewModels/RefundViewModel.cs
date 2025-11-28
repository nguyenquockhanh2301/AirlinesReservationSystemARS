using ARS.Models;

namespace ARS.ViewModels
{
    public class RefundViewModel
    {
        public Reservation? Reservation { get; set; }
        public int DaysBeforeDeparture { get; set; }
        public decimal PotentialRefundAmount { get; set; }
        public decimal RefundPercentage { get; set; }
    }
}
