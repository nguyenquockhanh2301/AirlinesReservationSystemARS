using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARS.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightSeatEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old index if it exists. MySQL does not support DROP INDEX IF EXISTS
            // outside of stored programs, so conditionally execute via information_schema.
            migrationBuilder.Sql(@"SET @exists := (SELECT COUNT(1) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Reservations' AND INDEX_NAME = 'IX_Reservation_Schedule_Seat_Unique');
SET @s = IF(@exists>0, 'ALTER TABLE `Reservations` DROP INDEX `IX_Reservation_Schedule_Seat_Unique`', 'SELECT 0');
PREPARE stmt FROM @s;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");

            migrationBuilder.AddColumn<int>(
                name: "FlightSeatId",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FlightSeats",
                columns: table => new
                {
                    FlightSeatId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    SeatId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                name: "IX_Reservation_Schedule_FlightSeat_Unique",
                table: "Reservations",
                columns: new[] { "ScheduleID", "FlightSeatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_FlightSeatId",
                table: "Reservations",
                column: "FlightSeatId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightSeats_ReservedByReservationID",
                table: "FlightSeats",
                column: "ReservedByReservationID");

            migrationBuilder.CreateIndex(
                name: "IX_FlightSeats_Schedule_Seat_Unique",
                table: "FlightSeats",
                columns: new[] { "ScheduleId", "SeatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlightSeats_SeatId",
                table: "FlightSeats",
                column: "SeatId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_FlightSeats_FlightSeatId",
                table: "Reservations",
                column: "FlightSeatId",
                principalTable: "FlightSeats",
                principalColumn: "FlightSeatId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_FlightSeats_FlightSeatId",
                table: "Reservations");

            migrationBuilder.DropTable(
                name: "FlightSeats");

            migrationBuilder.DropIndex(
                name: "IX_Reservation_Schedule_FlightSeat_Unique",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_FlightSeatId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "FlightSeatId",
                table: "Reservations");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_Schedule_Seat_Unique",
                table: "Reservations",
                columns: new[] { "ScheduleID", "SeatId" },
                unique: true);
        }
    }
}
