using Microsoft.EntityFrameworkCore;
using ARS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace ARS.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet properties for each entity
        public DbSet<City> Cities { get; set; }
    // IdentityDbContext already provides Users DbSet via IdentityDbContext<User,...>
        public DbSet<PricingPolicy> PricingPolicies { get; set; }
        public DbSet<Flight> Flights { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ReservationLeg> ReservationLegs { get; set; }
        public DbSet<FlightSeat> FlightSeats { get; set; }
    public DbSet<SeatLayout> SeatLayouts { get; set; }
    public DbSet<Seat> Seats { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Refund> Refunds { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Flight - City relationships (self-referencing)
            modelBuilder.Entity<Flight>()
                .HasOne(f => f.OriginCity)
                .WithMany(c => c.FlightsAsOrigin)
                .HasForeignKey(f => f.OriginCityID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Flight>()
                .HasOne(f => f.DestinationCity)
                .WithMany(c => c.FlightsAsDestination)
                .HasForeignKey(f => f.DestinationCityID)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Flight - PricingPolicy relationship
            modelBuilder.Entity<Flight>()
                .HasOne(f => f.PricingPolicy)
                .WithMany(p => p.Flights)
                .HasForeignKey(f => f.PolicyID)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Schedule - Flight relationship
            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Flight)
                .WithMany(f => f.Schedules)
                .HasForeignKey(s => s.FlightID)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Flight - SeatLayout relationship
            modelBuilder.Entity<Flight>()
                .HasOne(f => f.SeatLayout)
                .WithMany()
                .HasForeignKey(f => f.SeatLayoutId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure SeatLayout - Seat relationship
            modelBuilder.Entity<Seat>()
                .HasOne(s => s.SeatLayout)
                .WithMany(sl => sl.Seats)
                .HasForeignKey(s => s.SeatLayoutId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Schedule - City relationship (optional)
            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.City)
                .WithMany(c => c.Schedules)
                .HasForeignKey(s => s.CityID)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Reservation - User relationship
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reservations)
                .HasForeignKey(r => r.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Reservation - Flight relationship
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Flight)
                .WithMany(f => f.Reservations)
                .HasForeignKey(r => r.FlightID)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Reservation - Schedule relationship
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Schedule)
                .WithMany(s => s.Reservations)
                .HasForeignKey(r => r.ScheduleID)
                .OnDelete(DeleteBehavior.Restrict);

            // Reservation - Seat relationship
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Seat)
                .WithMany()
                .HasForeignKey(r => r.SeatId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ReservationLeg relationships
            modelBuilder.Entity<ReservationLeg>()
                .HasOne(rl => rl.Reservation)
                .WithMany(r => r.Legs)
                .HasForeignKey(rl => rl.ReservationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReservationLeg>()
                .HasOne(rl => rl.Flight)
                .WithMany()
                .HasForeignKey(rl => rl.FlightID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReservationLeg>()
                .HasOne(rl => rl.Schedule)
                .WithMany()
                .HasForeignKey(rl => rl.ScheduleID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReservationLeg>()
                .HasOne(rl => rl.Seat)
                .WithMany()
                .HasForeignKey(rl => rl.SeatId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Payment - Reservation relationship
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Reservation)
                .WithMany(r => r.Payments)
                .HasForeignKey(p => p.ReservationID)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Refund - Reservation relationship
            modelBuilder.Entity<Refund>()
                .HasOne(r => r.Reservation)
                .WithMany(res => res.Refunds)
                .HasForeignKey(r => r.ReservationID)
                .OnDelete(DeleteBehavior.Cascade);

            // Add unique constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Flight>()
                .HasIndex(f => f.FlightNumber)
                .IsUnique();

            modelBuilder.Entity<Reservation>()
                .HasIndex(r => r.ConfirmationNumber)
                .IsUnique();

            // Ensure a flight-seat cannot be double-booked for the same schedule
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.FlightSeat)
                .WithMany()
                .HasForeignKey(r => r.FlightSeatId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Reservation>()
                .HasIndex(r => new { r.ScheduleID, r.FlightSeatId })
                .IsUnique()
                .HasDatabaseName("IX_Reservation_Schedule_FlightSeat_Unique");

            modelBuilder.Entity<City>()
                .HasIndex(c => c.AirportCode)
                .IsUnique();

            // Configure FlightSeat - per-schedule seat status
            modelBuilder.Entity<FlightSeat>(eb =>
            {
                eb.HasIndex(fs => new { fs.ScheduleId, fs.SeatId })
                    .IsUnique()
                    .HasDatabaseName("IX_FlightSeats_Schedule_Seat_Unique");

                eb.HasOne(fs => fs.Schedule)
                    .WithMany(s => s.FlightSeats)
                    .HasForeignKey(fs => fs.ScheduleId)
                    .OnDelete(DeleteBehavior.Cascade);

                eb.HasOne(fs => fs.AircraftSeat)
                    .WithMany()
                    .HasForeignKey(fs => fs.SeatId)
                    .OnDelete(DeleteBehavior.Restrict);

                eb.HasOne(fs => fs.ReservedByReservation)
                    .WithMany()
                    .HasForeignKey(fs => fs.ReservedByReservationID)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Seed initial data (optional)
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed sample cities
            modelBuilder.Entity<City>().HasData(
                new City { CityID = 1, CityName = "Manila", Country = "Philippines", AirportCode = "MNL" },
                new City { CityID = 2, CityName = "Cebu", Country = "Philippines", AirportCode = "CEB" },
                new City { CityID = 3, CityName = "Tokyo", Country = "Japan", AirportCode = "NRT" },
                new City { CityID = 4, CityName = "Singapore", Country = "Singapore", AirportCode = "SIN" },
                new City { CityID = 5, CityName = "Hong Kong", Country = "Hong Kong", AirportCode = "HKG" }
            );

            // Seed pricing policies
            modelBuilder.Entity<PricingPolicy>().HasData(
                new PricingPolicy { PolicyID = 1, Description = "Early Bird (30+ days)", DaysBeforeDeparture = 30, PriceMultiplier = 0.80m },
                new PricingPolicy { PolicyID = 2, Description = "Standard (15-29 days)", DaysBeforeDeparture = 15, PriceMultiplier = 1.00m },
                new PricingPolicy { PolicyID = 3, Description = "Late Booking (7-14 days)", DaysBeforeDeparture = 7, PriceMultiplier = 1.20m },
                new PricingPolicy { PolicyID = 4, Description = "Last Minute (0-6 days)", DaysBeforeDeparture = 0, PriceMultiplier = 1.50m }
            );

            // NOTE: Admin user creation should be done via a secure seed/migration step using UserManager
            // Avoid seeding plaintext passwords here.
        }
    }
}
