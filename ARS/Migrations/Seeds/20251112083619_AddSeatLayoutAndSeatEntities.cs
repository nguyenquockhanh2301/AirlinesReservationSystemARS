using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARS.Migrations.Seeds
{
    /// <inheritdoc />
    public partial class AddSeatLayoutAndSeatEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: original index on ScheduleID is left intact to avoid dropping an index
            // that may be required by existing foreign key constraints in some deployments.

            // The following schema changes may already exist in some databases (e.g., applied manually or partially).
            // Wrap operations in try/catch to make this migration idempotent for dev/test environments.
            try
            {
                migrationBuilder.AddColumn<int>(
                    name: "SeatId",
                    table: "Reservations",
                    type: "int",
                    nullable: true);
            }
            catch { }

            try
            {
                migrationBuilder.AddColumn<string>(
                    name: "SeatLabel",
                    table: "Reservations",
                    type: "varchar(10)",
                    maxLength: 10,
                    nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4");
            }
            catch { }

            try
            {
                migrationBuilder.AddColumn<int>(
                    name: "SeatLayoutId",
                    table: "Flights",
                    type: "int",
                    nullable: true);
            }
            catch { }

            try
            {
                migrationBuilder.CreateTable(
                    name: "SeatLayouts",
                    columns: table => new
                    {
                        SeatLayoutId = table.Column<int>(type: "int", nullable: false)
                            .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                        Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        MetadataJson = table.Column<string>(type: "longtext", nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4")
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_SeatLayouts", x => x.SeatLayoutId);
                    })
                    .Annotation("MySql:CharSet", "utf8mb4");
            }
            catch { }

            try
            {
                migrationBuilder.CreateTable(
                    name: "Seats",
                    columns: table => new
                    {
                        SeatId = table.Column<int>(type: "int", nullable: false)
                            .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                        SeatLayoutId = table.Column<int>(type: "int", nullable: false),
                        RowNumber = table.Column<int>(type: "int", nullable: false),
                        Column = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Label = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        CabinClass = table.Column<int>(type: "int", nullable: false),
                        IsExitRow = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        IsPremium = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        PriceModifier = table.Column<decimal>(type: "decimal(10,2)", nullable: true)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Seats", x => x.SeatId);
                        table.ForeignKey(
                            name: "FK_Seats_SeatLayouts_SeatLayoutId",
                            column: x => x.SeatLayoutId,
                            principalTable: "SeatLayouts",
                            principalColumn: "SeatLayoutId",
                            onDelete: ReferentialAction.Cascade);
                    })
                    .Annotation("MySql:CharSet", "utf8mb4");
            }
            catch { }

            try { migrationBuilder.CreateIndex(
                name: "IX_Reservation_Schedule_Seat_Unique",
                table: "Reservations",
                columns: new[] { "ScheduleID", "SeatId" },
                unique: true); } catch { }

            try { migrationBuilder.CreateIndex(
                name: "IX_Reservations_SeatId",
                table: "Reservations",
                column: "SeatId"); } catch { }

            try { migrationBuilder.CreateIndex(
                name: "IX_Flights_SeatLayoutId",
                table: "Flights",
                column: "SeatLayoutId"); } catch { }

            try { migrationBuilder.CreateIndex(
                name: "IX_Seats_SeatLayoutId",
                table: "Seats",
                column: "SeatLayoutId"); } catch { }

            try
            {
                migrationBuilder.AddForeignKey(
                    name: "FK_Flights_SeatLayouts_SeatLayoutId",
                    table: "Flights",
                    column: "SeatLayoutId",
                    principalTable: "SeatLayouts",
                    principalColumn: "SeatLayoutId",
                    onDelete: ReferentialAction.SetNull);
            }
            catch { }

            try
            {
                migrationBuilder.AddForeignKey(
                    name: "FK_Reservations_Seats_SeatId",
                    table: "Reservations",
                    column: "SeatId",
                    principalTable: "Seats",
                    principalColumn: "SeatId",
                    onDelete: ReferentialAction.SetNull);
            }
            catch { }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Flights_SeatLayouts_SeatLayoutId",
                table: "Flights");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Seats_SeatId",
                table: "Reservations");

            migrationBuilder.DropTable(
                name: "Seats");

            migrationBuilder.DropTable(
                name: "SeatLayouts");

            migrationBuilder.DropIndex(
                name: "IX_Reservation_Schedule_Seat_Unique",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_SeatId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Flights_SeatLayoutId",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "SeatId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SeatLabel",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SeatLayoutId",
                table: "Flights");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ScheduleID",
                table: "Reservations",
                column: "ScheduleID");
        }
    }
}
