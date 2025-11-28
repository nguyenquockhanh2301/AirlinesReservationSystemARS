using System.ComponentModel.DataAnnotations;

namespace ARS.ViewModels
{
    public class FlightSearchViewModel
    {
        [Display(Name = "From")]
        public int? OriginCityID { get; set; }

        [Display(Name = "To")]
        public int? DestinationCityID { get; set; }

        [Required(ErrorMessage = "Please select travel date")]
        [Display(Name = "Travel Date")]
        [DataType(DataType.Date)]
        public DateOnly TravelDate { get; set; } = DateOnly.FromDateTime(DateTime.Now.AddDays(1));

        [Display(Name = "Number of Passengers")]
        [Range(1, 10, ErrorMessage = "Number of passengers must be between 1 and 10")]
        public int Passengers { get; set; } = 1;

        [Display(Name = "Class")]
        public string Class { get; set; } = "Economy";

        [Display(Name = "Trip Type")]
        public string TripType { get; set; } = "OneWay"; // OneWay, RoundTrip, MultiCity

        [Display(Name = "Return Date")]
        [DataType(DataType.Date)]
        public DateOnly? ReturnDate { get; set; }

        // Multi-city legs (model binding supports indexed names like Legs[0].OriginCityID)
        public List<MultiCityLegViewModel>? Legs { get; set; }

        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 8;

        // Round-trip pagination
        public int OutboundPage { get; set; } = 1;
        public int ReturnPage { get; set; } = 1;
    }

    public class MultiCityLegViewModel
    {
        [Display(Name = "From")]
        public int OriginCityID { get; set; }
        [Display(Name = "To")]
        public int DestinationCityID { get; set; }
        [Display(Name = "Travel Date")]
        [DataType(DataType.Date)]
        public DateOnly TravelDate { get; set; } = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
    }
}
