using ARS.Models;

namespace ARS.ViewModels
{
    public class PaymentViewModel
    {
        public Reservation Reservation { get; set; } = null!;
        public decimal TotalPaid { get; set; }
        public decimal AmountDue { get; set; }
        public List<Payment> PendingPayments { get; set; } = new List<Payment>();
        public string PayPalClientId { get; set; } = string.Empty;
    }
}
