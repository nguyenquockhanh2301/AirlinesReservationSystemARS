using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    public class Refund
    {
        [Key]
        public int RefundID { get; set; }

        [Required]
        [ForeignKey("Reservation")]
        public int ReservationID { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal RefundAmount { get; set; }

        [Required]
        public DateTime RefundDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal RefundPercentage { get; set; }

        // Navigation properties
        public virtual Reservation? Reservation { get; set; }
    }
}
