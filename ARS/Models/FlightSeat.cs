using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    public enum FlightSeatStatus
    {
        Available,
        Reserved,
        Blocked
    }

    public class FlightSeat
    {
        [Key]
        public int FlightSeatId { get; set; }

        [Required]
        public int ScheduleId { get; set; }

        [ForeignKey("ScheduleId")]
        public Schedule? Schedule { get; set; }

        [Required]
        public int SeatId { get; set; } // FK to Aircraft seat template (Seats table)

        [ForeignKey("SeatId")]
        public Seat? AircraftSeat { get; set; }

        [Required]
        public FlightSeatStatus Status { get; set; } = FlightSeatStatus.Available;

        public int? ReservedByReservationID { get; set; }

        [ForeignKey("ReservedByReservationID")]
        public Reservation? ReservedByReservation { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? Price { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
