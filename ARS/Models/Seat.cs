using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    public enum CabinClass
    {
        First,
        Business,
        Economy
    }

    public class Seat
    {
        [Key]
        public int SeatId { get; set; }

        [Required]
        public int SeatLayoutId { get; set; }

        [ForeignKey("SeatLayoutId")]
        public SeatLayout? SeatLayout { get; set; }

        [Required]
        public int RowNumber { get; set; }

        [Required]
        [StringLength(5)]
        public string Column { get; set; } = string.Empty; // A, B, C

        [Required]
        [StringLength(10)]
        public string Label { get; set; } = string.Empty; // e.g. "10A"

        [Required]
        public CabinClass CabinClass { get; set; } = CabinClass.Economy;

        // Optional extras
        public bool IsExitRow { get; set; }
        public bool IsPremium { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceModifier { get; set; }
    }
}
