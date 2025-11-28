using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ARS.Models
{
    public class User : IdentityUser<int>
    {
        // Backwards-compatible accessor for code that expects UserID
        [NotMapped]
        public int UserID { get => Id; set => Id = value; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

    // Email and Password are provided by IdentityUser<int>
    // Identity stores Email and PasswordHash; avoid duplicating properties here.

        [Phone]
        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [Required]
        [StringLength(1)]
        public char Gender { get; set; } // M, F, O

        public int? Age { get; set; }

        [StringLength(20)]
        public string? CreditCardNumber { get; set; }

        public int SkyMiles { get; set; } = 0;

    // Role is managed via IdentityRole<int>. Keep a convenience property for legacy code.
    [NotMapped]
    public string Role { get; set; } = "Customer"; // Customer, Admin

        // Navigation properties
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
