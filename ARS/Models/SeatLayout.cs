using System.ComponentModel.DataAnnotations;

namespace ARS.Models
{
    public class SeatLayout
    {
        [Key]
        public int SeatLayoutId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty; // e.g. "A320-180"

        // optional metadata for rendering
        public string? MetadataJson { get; set; }

        public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
    }
}
