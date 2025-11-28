using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARS.Migrations
{
    /// <inheritdoc />
    public partial class SeedFlightSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Populate FlightSeats for existing schedules by copying template seats from SeatLayouts
            migrationBuilder.Sql(@"
                INSERT INTO `FlightSeats` (`ScheduleId`, `SeatId`, `Status`, `Price`, `CreatedAt`)
                SELECT s.`ScheduleID`, se.`SeatId`, 'Available', NULL, NOW()
                FROM `Schedules` s
                JOIN `Flights` f ON s.`FlightID` = f.`FlightID`
                JOIN `Seats` se ON se.`SeatLayoutId` = f.`SeatLayoutId`
                WHERE f.`SeatLayoutId` IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM `FlightSeats` fs WHERE fs.`ScheduleId` = s.`ScheduleID` AND fs.`SeatId` = se.`SeatId`
                  );
            ");

            // Map existing reservations to flight seats (if reservations already selected a template SeatId)
            // Update reservation's FlightSeatId where a matching flight-seat exists and mark the flight-seat reserved.
            migrationBuilder.Sql(@"
                UPDATE `Reservations` r
                JOIN `FlightSeats` fs ON r.`ScheduleID` = fs.`ScheduleId` AND r.`SeatId` = fs.`SeatId`
                SET r.`FlightSeatId` = fs.`FlightSeatId`, fs.`ReservedByReservationID` = r.`ReservationID`, fs.`Status` = 'Reserved'
                WHERE r.`SeatId` IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
