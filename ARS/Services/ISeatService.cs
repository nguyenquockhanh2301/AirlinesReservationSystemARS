using System.Collections.Generic;
using System.Threading.Tasks;
using ARS.Models;

namespace ARS.Services
{
    public class FlightSeatDto
    {
        public int FlightSeatId { get; set; }
        public int SeatId { get; set; }
        public string Label { get; set; } = string.Empty;
        public int RowNumber { get; set; }
        public string Column { get; set; } = string.Empty;
        public CabinClass CabinClass { get; set; }
        public decimal? Price { get; set; }
        public bool IsAvailable { get; set; }
    }

    public interface ISeatService
    {
        Task GenerateFlightSeatsAsync(int scheduleId);
        Task<List<FlightSeatDto>> GetAvailableSeatsAsync(int scheduleId);
        Task<bool> ReserveSeatAsync(int flightSeatId, int reservationId);
        // Reserve a seat for a specific reservation leg (for multi-leg/round-trip support)
        Task<bool> ReserveSeatForLegAsync(int flightSeatId, int reservationLegId);
        Task<bool> CancelReservationSeatAsync(int reservationId);
        // Cancel a reservation seat for a specific reservation leg
        Task<bool> CancelReservationSeatForLegAsync(int reservationLegId);
    }
}
