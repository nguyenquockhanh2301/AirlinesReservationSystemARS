using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARS.Migrations.Seeds
{
    public partial class AddFlightSeatIdToReservations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add FlightSeatId column to Reservations
            migrationBuilder.AddColumn<int>(
                name: "FlightSeatId",
                table: "Reservations",
                type: "int",
                nullable: true);

            // Drop old unique index if present (from previous seat design)
            try
            {
                migrationBuilder.DropIndex(
                    name: "IX_Reservation_Schedule_Seat_Unique",
                    table: "Reservations");
            }
            catch
            {
                // Ignore if index does not exist in some deployments
            }

            // Backfill FlightSeatId by matching Reservation.SeatId and ScheduleID
            migrationBuilder.Sql(@"
                UPDATE `Reservations` r
                JOIN `FlightSeats` fs ON fs.`ScheduleId` = r.`ScheduleID` AND fs.`SeatId` = r.`SeatId`
                SET r.`FlightSeatId` = fs.`FlightSeatId`;
            ");

            // Set FlightSeats.ReservedByReservationID for mapped reservations
            migrationBuilder.Sql(@"
                UPDATE `FlightSeats` fs
                JOIN `Reservations` r ON r.`FlightSeatId` = fs.`FlightSeatId`
                SET fs.`ReservedByReservationID` = r.`ReservationID`, fs.`Status` = 'Reserved'
                WHERE fs.`ReservedByReservationID` IS NULL;
            ");

            // Create new unique index on ScheduleID + FlightSeatId
            migrationBuilder.CreateIndex(
                name: "IX_Reservation_Schedule_FlightSeat_Unique",
                table: "Reservations",
                columns: new[] { "ScheduleID", "FlightSeatId" },
                unique: true);

            // Add foreign key from Reservations to FlightSeats
            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_FlightSeats_FlightSeatId",
                table: "Reservations",
                column: "FlightSeatId",
                principalTable: "FlightSeats",
                principalColumn: "FlightSeatId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove foreign key
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_FlightSeats_FlightSeatId",
                table: "Reservations");

            // Drop new index
            migrationBuilder.DropIndex(
                name: "IX_Reservation_Schedule_FlightSeat_Unique",
                table: "Reservations");

            // Drop FlightSeatId column
            migrationBuilder.DropColumn(
                name: "FlightSeatId",
                table: "Reservations");

            // Re-create old unique index (best-effort)
            migrationBuilder.CreateIndex(
                name: "IX_Reservation_Schedule_Seat_Unique",
                table: "Reservations",
                columns: new[] { "ScheduleID", "SeatId" },
                unique: true);
        }
    }
}
