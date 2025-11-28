using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    public class Payment
    {
        [Key]
        public int PaymentID { get; set; }

        [Required]
        [ForeignKey("Reservation")]
        public int ReservationID { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = string.Empty; // CreditCard, DebitCard, PayPal, etc.

        [Required]
        [StringLength(50)]
        public string TransactionStatus { get; set; } = "Pending"; // Pending, Completed, Failed, Refunded

        [StringLength(100)]
        public string? TransactionRefNo { get; set; }

        // Navigation properties
        public virtual Reservation? Reservation { get; set; }
    }
}
