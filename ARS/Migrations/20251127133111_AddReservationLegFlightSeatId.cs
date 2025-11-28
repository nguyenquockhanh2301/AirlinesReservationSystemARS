using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARS.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationLegFlightSeatId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ScheduleID",
                table: "Reservations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "FlightID",
                table: "Reservations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // ReservationLegs table already exists in the database; only add the new FlightSeatId column
            migrationBuilder.AddColumn<int>(
                name: "FlightSeatId",
                table: "ReservationLegs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLegs_FlightSeatId",
                table: "ReservationLegs",
                column: "FlightSeatId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReservationLegs_FlightSeats_FlightSeatId",
                table: "ReservationLegs",
                column: "FlightSeatId",
                principalTable: "FlightSeats",
                principalColumn: "FlightSeatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the FlightSeatId column, index and FK added in Up
            migrationBuilder.DropForeignKey(
                name: "FK_ReservationLegs_FlightSeats_FlightSeatId",
                table: "ReservationLegs");

            migrationBuilder.DropIndex(
                name: "IX_ReservationLegs_FlightSeatId",
                table: "ReservationLegs");

            migrationBuilder.DropColumn(
                name: "FlightSeatId",
                table: "ReservationLegs");

            migrationBuilder.AlterColumn<int>(
                name: "ScheduleID",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FlightID",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
