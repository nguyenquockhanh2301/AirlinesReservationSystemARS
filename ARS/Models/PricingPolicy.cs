using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    public class PricingPolicy
    {
        [Key]
        public int PolicyID { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public int DaysBeforeDeparture { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal PriceMultiplier { get; set; }

        // Navigation properties
        public virtual ICollection<Flight> Flights { get; set; } = new List<Flight>();
    }
}
