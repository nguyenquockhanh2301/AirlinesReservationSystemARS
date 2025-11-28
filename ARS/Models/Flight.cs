using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    public class Flight
    {
        [Key]
        public int FlightID { get; set; }

        [Required]
        [StringLength(20)]
        public string FlightNumber { get; set; } = string.Empty;

        [Required]
        [ForeignKey("OriginCity")]
        public int OriginCityID { get; set; }

        [Required]
        [ForeignKey("DestinationCity")]
        public int DestinationCityID { get; set; }

        [Required]
        public DateTime DepartureTime { get; set; }

        [Required]
        public DateTime ArrivalTime { get; set; }

        [Required]
        public int Duration { get; set; } // Duration in minutes

        [Required]
        [StringLength(50)]
        public string AircraftType { get; set; } = string.Empty;

        [Required]
        public int TotalSeats { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal BaseFare { get; set; }

    // Optional link to a seat layout (for aircraft-specific seat maps)
    public int? SeatLayoutId { get; set; }
    public virtual SeatLayout? SeatLayout { get; set; }

        // Foreign key to PricingPolicy (optional)
        public int? PolicyID { get; set; }

        // Navigation properties
        public virtual City? OriginCity { get; set; }
        public virtual City? DestinationCity { get; set; }
        public virtual PricingPolicy? PricingPolicy { get; set; }
        public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
