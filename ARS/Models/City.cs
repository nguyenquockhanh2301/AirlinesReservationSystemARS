using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    public class City
    {
        [Key]
        public int CityID { get; set; }

        [Required]
        [StringLength(100)]
        public string CityName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Country { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string AirportCode { get; set; } = string.Empty;

        // Navigation properties
        public virtual ICollection<Flight> FlightsAsOrigin { get; set; } = new List<Flight>();
        public virtual ICollection<Flight> FlightsAsDestination { get; set; } = new List<Flight>();
        public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }
}
