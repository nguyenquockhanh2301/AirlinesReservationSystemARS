using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARS.Migrations.Seeds
{
    /// <inheritdoc />
    public partial class SeedFlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert sample flights for development/testing
            migrationBuilder.InsertData(
                table: "Flights",
                columns: new[] { "FlightID", "FlightNumber", "OriginCityID", "DestinationCityID", "DepartureTime", "ArrivalTime", "Duration", "AircraftType", "TotalSeats", "BaseFare", "PolicyID" },
                values: new object[,]
                {
                    { 100, "ARS100", 1, 2, new DateTime(2025, 11, 15, 8, 0, 0), new DateTime(2025, 11, 15, 9, 0, 0), 60, "A320", 150, 50.00m, 2 },
                    { 101, "ARS200", 1, 3, new DateTime(2025, 11, 16, 10, 30, 0), new DateTime(2025, 11, 16, 18, 0, 0), 450, "B787", 300, 320.00m, 3 },
                    { 102, "ARS300", 2, 4, new DateTime(2025, 11, 17, 14, 0, 0), new DateTime(2025, 11, 17, 16, 30, 0), 150, "A321", 180, 120.00m, 2 }
                });

            // Insert corresponding schedules
            migrationBuilder.InsertData(
                table: "Schedules",
                columns: new[] { "ScheduleID", "FlightID", "Date", "Status", "CityID" },
                values: new object[,]
                {
                    { 1000, 100, new DateTime(2025, 11, 15), "Scheduled", 2 },
                    { 1001, 101, new DateTime(2025, 11, 16), "Scheduled", 3 },
                    { 1002, 102, new DateTime(2025, 11, 17), "Scheduled", 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Schedules",
                keyColumn: "ScheduleID",
                keyValues: new object[] { 1000, 1001, 1002 });

            migrationBuilder.DeleteData(
                table: "Flights",
                keyColumn: "FlightID",
                keyValues: new object[] { 100, 101, 102 });
        }
    }
}
