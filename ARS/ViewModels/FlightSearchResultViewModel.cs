using ARS.Models;

namespace ARS.ViewModels
{
    public class FlightSearchResultViewModel
    {
        public FlightSearchViewModel SearchCriteria { get; set; } = new FlightSearchViewModel();
        // For One-way searches this contains matching flights
        public List<FlightResultItem> Flights { get; set; } = new List<FlightResultItem>();

        // For round-trip
        public List<FlightResultItem> OutboundFlights { get; set; } = new List<FlightResultItem>();
        public List<FlightResultItem> ReturnFlights { get; set; } = new List<FlightResultItem>();

        // For multi-city: a list of result lists, one per leg
        public List<List<FlightResultItem>> LegsResults { get; set; } = new List<List<FlightResultItem>>();

        // Pagination
        public int TotalResults { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 8;
        public int TotalPages => (int)Math.Ceiling((double)TotalResults / PageSize);

        // Round-trip pagination
        public int OutboundTotalResults { get; set; }
        public int OutboundCurrentPage { get; set; } = 1;
        public int OutboundPageSize { get; set; } = 2;
        public int OutboundTotalPages => (int)Math.Ceiling((double)OutboundTotalResults / OutboundPageSize);

        public int ReturnTotalResults { get; set; }
        public int ReturnCurrentPage { get; set; } = 1;
        public int ReturnPageSize { get; set; } = 2;
        public int ReturnTotalPages => (int)Math.Ceiling((double)ReturnTotalResults / ReturnPageSize);
    }

    public class FlightResultItem
    {
        public int FlightID { get; set; }
        public string FlightNumber { get; set; } = string.Empty;
        public string OriginCity { get; set; } = string.Empty;
        public string OriginAirportCode { get; set; } = string.Empty;
        public string DestinationCity { get; set; } = string.Empty;
        public string DestinationAirportCode { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public int Duration { get; set; }
        public string AircraftType { get; set; } = string.Empty;
        public int AvailableSeats { get; set; }
        public decimal BasePrice { get; set; }
        public decimal FinalPrice { get; set; }
        public int? ScheduleID { get; set; }
    }
}
