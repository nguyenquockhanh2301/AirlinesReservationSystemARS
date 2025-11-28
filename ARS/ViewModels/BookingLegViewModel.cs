namespace ARS.ViewModels
{
    public class BookingLegViewModel
    {
        public int FlightID { get; set; }
        public int? ScheduleID { get; set; }
        public string FlightNumber { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public DateOnly TravelDate { get; set; }
        public decimal BasePrice { get; set; }
        public int LegOrder { get; set; }
        public int Duration { get; set; }
        public string AircraftType { get; set; } = string.Empty;

        // Seat selection for this leg
        public string? SelectedSeat { get; set; }
        public int? SelectedSeatId { get; set; }
    }
}
