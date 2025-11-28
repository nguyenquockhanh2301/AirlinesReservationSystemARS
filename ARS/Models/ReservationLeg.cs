using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    public class ReservationLeg
    {
        [Key]
        public int ReservationLegID { get; set; }

        [Required]
        [ForeignKey("Reservation")]
        public int ReservationID { get; set; }

        [Required]
        [ForeignKey("Flight")]
        public int FlightID { get; set; }

        [Required]
        [ForeignKey("Schedule")]
        public int ScheduleID { get; set; }

        [Required]
        public DateOnly TravelDate { get; set; }

        [Required]
        public int LegOrder { get; set; } // 1 for outbound/first leg, 2 for return/second leg, etc.

        // Seat assignment (optional, per leg)
        public int? SeatId { get; set; }
        public virtual Seat? Seat { get; set; }

        // Reference to the per-schedule FlightSeat (for per-leg reservations)
        public int? FlightSeatId { get; set; }
        public virtual FlightSeat? FlightSeat { get; set; }

        // Legacy label for compatibility (e.g. "12A")
        [StringLength(10)]
        public string? SeatLabel { get; set; }

        // Navigation properties
        public virtual Reservation? Reservation { get; set; }
        public virtual Flight? Flight { get; set; }
        public virtual Schedule? Schedule { get; set; }
    }
}
