using ARS.Models;
using System.Collections.Generic;

namespace ARS.ViewModels
{
    public class RescheduleInitViewModel
    {
        public Reservation Reservation { get; set; } = new Reservation();
        public DateOnly NewDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    }

    public class RescheduleSearchResultViewModel
    {
        public Reservation Reservation { get; set; } = new Reservation();
        public DateOnly NewDate { get; set; }
        public int Passengers { get; set; }
        public string Class { get; set; } = "Economy";
        public List<ARS.ViewModels.FlightResultItem> Flights { get; set; } = new List<ARS.ViewModels.FlightResultItem>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 5;
    }
}
