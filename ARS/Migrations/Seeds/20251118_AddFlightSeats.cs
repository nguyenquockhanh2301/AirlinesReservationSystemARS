using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARS.Migrations.Seeds
{
    public partial class AddFlightSeats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlightSeats",
                columns: table => new
                {
                    FlightSeatId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    SeatId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ReservedByReservationID = table.Column<int>(type: "int", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightSeats", x => x.FlightSeatId);
                    table.ForeignKey(
                        name: "FK_FlightSeats_Reservations_ReservedByReservationID",
                        column: x => x.ReservedByReservationID,
                        principalTable: "Reservations",
                        principalColumn: "ReservationID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FlightSeats_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "ScheduleID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlightSeats_Seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "SeatId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FlightSeats_Schedule_Seat_Unique",
                table: "FlightSeats",
                columns: new[] { "ScheduleId", "SeatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlightSeats_SeatId",
                table: "FlightSeats",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightSeats_ScheduleId",
                table: "FlightSeats",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightSeats_ReservedByReservationID",
                table: "FlightSeats",
                column: "ReservedByReservationID");

            // Populate FlightSeats for existing schedules by copying template seats from SeatLayouts
            migrationBuilder.Sql(@"
                INSERT INTO `FlightSeats` (`ScheduleId`, `SeatId`, `Status`, `Price`, `CreatedAt`)
                SELECT s.`ScheduleID`, se.`SeatId`, 'Available', NULL, NOW()
                FROM `Schedules` s
                JOIN `Flights` f ON s.`FlightID` = f.`FlightID`
                JOIN `Seats` se ON se.`SeatLayoutId` = f.`SeatLayoutId`
                WHERE f.`SeatLayoutId` IS NOT NULL;
            ");

            // Map existing reservations to flight seats (if reservations already selected a template SeatId)
            migrationBuilder.Sql(@"
                UPDATE `FlightSeats` fs
                JOIN `Reservations` r ON r.`ScheduleID` = fs.`ScheduleId` AND r.`SeatId` = fs.`SeatId`
                SET fs.`ReservedByReservationID` = r.`ReservationID`, fs.`Status` = 'Reserved'
                WHERE r.`SeatId` IS NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlightSeats");
        }
    }
}
