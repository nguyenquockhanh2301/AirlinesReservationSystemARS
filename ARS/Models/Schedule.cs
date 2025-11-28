using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    public class Schedule
    {
        [Key]
        public int ScheduleID { get; set; }

        [Required]
        [ForeignKey("Flight")]
        public int FlightID { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Scheduled"; // Scheduled, Delayed, Cancelled, Completed

        // Foreign key to City (optional - for additional routing info)
        public int? CityID { get; set; }

        // Navigation properties
        public virtual Flight? Flight { get; set; }
        public virtual City? City { get; set; }
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public virtual ICollection<FlightSeat> FlightSeats { get; set; } = new List<FlightSeat>();
    }
}
