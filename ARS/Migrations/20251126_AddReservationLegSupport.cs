using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARS.Migrations
{
    /// <summary>
    /// Migration to add multi-leg reservation support
    /// </summary>
    public partial class AddReservationLegSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make FlightID and ScheduleID nullable in Reservations table for backward compatibility
            migrationBuilder.AlterColumn<int>(
                name: "FlightID",
                table: "Reservations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ScheduleID",
                table: "Reservations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Create ReservationLegs table
            migrationBuilder.CreateTable(
                name: "ReservationLegs",
                columns: table => new
                {
                    ReservationLegID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReservationID = table.Column<int>(type: "int", nullable: false),
                    FlightID = table.Column<int>(type: "int", nullable: false),
                    ScheduleID = table.Column<int>(type: "int", nullable: false),
                    TravelDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LegOrder = table.Column<int>(type: "int", nullable: false),
                    SeatId = table.Column<int>(type: "int", nullable: true),
                    SeatLabel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationLegs", x => x.ReservationLegID);
                    table.ForeignKey(
                        name: "FK_ReservationLegs_Reservations_ReservationID",
                        column: x => x.ReservationID,
                        principalTable: "Reservations",
                        principalColumn: "ReservationID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReservationLegs_Flights_FlightID",
                        column: x => x.FlightID,
                        principalTable: "Flights",
                        principalColumn: "FlightID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationLegs_Schedules_ScheduleID",
                        column: x => x.ScheduleID,
                        principalTable: "Schedules",
                        principalColumn: "ScheduleID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationLegs_Seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "SeatId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLegs_ReservationID",
                table: "ReservationLegs",
                column: "ReservationID");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLegs_FlightID",
                table: "ReservationLegs",
                column: "FlightID");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLegs_ScheduleID",
                table: "ReservationLegs",
                column: "ScheduleID");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLegs_SeatId",
                table: "ReservationLegs",
                column: "SeatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationLegs");

            migrationBuilder.AlterColumn<int>(
                name: "FlightID",
                table: "Reservations",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ScheduleID",
                table: "Reservations",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
