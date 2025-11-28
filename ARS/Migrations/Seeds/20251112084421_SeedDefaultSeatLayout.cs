using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARS.Migrations.Seeds
{
    /// <inheritdoc />
    public partial class SeedDefaultSeatLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create a default 6-across x 40-rows seat layout (rows 1-10 First, 11-30 Business, 31-40 Economy)
            // Ensure SeatLayouts table exists (no-op if created by prior migration)
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `SeatLayouts` (
                `SeatLayoutId` int NOT NULL AUTO_INCREMENT,
                `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `MetadataJson` longtext CHARACTER SET utf8mb4 NULL,
                PRIMARY KEY (`SeatLayoutId`)
            ) CHARACTER SET=utf8mb4;");

            migrationBuilder.Sql("INSERT INTO `SeatLayouts` (`Name`) VALUES ('Default-6x40');");

            // Insert seats for rows 1..40 and columns A..F
            for (int r = 1; r <= 40; r++)
            {
                var cabin = (r <= 10) ? 0 : ((r <= 30) ? 1 : 2);
                // A..F
                migrationBuilder.Sql($"INSERT INTO `Seats` (`SeatLayoutId`, `RowNumber`, `Column`, `Label`, `CabinClass`, `IsExitRow`, `IsPremium`, `PriceModifier`) SELECT SeatLayoutId, {r}, 'A', '{r}A', {cabin}, 0, 0, NULL FROM SeatLayouts WHERE Name='Default-6x40' LIMIT 1;");
                migrationBuilder.Sql($"INSERT INTO `Seats` (`SeatLayoutId`, `RowNumber`, `Column`, `Label`, `CabinClass`, `IsExitRow`, `IsPremium`, `PriceModifier`) SELECT SeatLayoutId, {r}, 'B', '{r}B', {cabin}, 0, 0, NULL FROM SeatLayouts WHERE Name='Default-6x40' LIMIT 1;");
                migrationBuilder.Sql($"INSERT INTO `Seats` (`SeatLayoutId`, `RowNumber`, `Column`, `Label`, `CabinClass`, `IsExitRow`, `IsPremium`, `PriceModifier`) SELECT SeatLayoutId, {r}, 'C', '{r}C', {cabin}, 0, 0, NULL FROM SeatLayouts WHERE Name='Default-6x40' LIMIT 1;");
                migrationBuilder.Sql($"INSERT INTO `Seats` (`SeatLayoutId`, `RowNumber`, `Column`, `Label`, `CabinClass`, `IsExitRow`, `IsPremium`, `PriceModifier`) SELECT SeatLayoutId, {r}, 'D', '{r}D', {cabin}, 0, 0, NULL FROM SeatLayouts WHERE Name='Default-6x40' LIMIT 1;");
                migrationBuilder.Sql($"INSERT INTO `Seats` (`SeatLayoutId`, `RowNumber`, `Column`, `Label`, `CabinClass`, `IsExitRow`, `IsPremium`, `PriceModifier`) SELECT SeatLayoutId, {r}, 'E', '{r}E', {cabin}, 0, 0, NULL FROM SeatLayouts WHERE Name='Default-6x40' LIMIT 1;");
                migrationBuilder.Sql($"INSERT INTO `Seats` (`SeatLayoutId`, `RowNumber`, `Column`, `Label`, `CabinClass`, `IsExitRow`, `IsPremium`, `PriceModifier`) SELECT SeatLayoutId, {r}, 'F', '{r}F', {cabin}, 0, 0, NULL FROM SeatLayouts WHERE Name='Default-6x40' LIMIT 1;");
            }

            // Assign this default layout to flights that don't have a layout yet
            migrationBuilder.Sql("UPDATE `Flights` SET `SeatLayoutId` = (SELECT SeatLayoutId FROM SeatLayouts WHERE Name='Default-6x40' LIMIT 1) WHERE SeatLayoutId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove assignment from flights
            migrationBuilder.Sql("UPDATE `Flights` SET `SeatLayoutId` = NULL WHERE SeatLayoutId = (SELECT SeatLayoutId FROM SeatLayouts WHERE Name='Default-6x40' LIMIT 1);");

            // Delete seeded seats and layout
            migrationBuilder.Sql("DELETE FROM `Seats` WHERE SeatLayoutId IN (SELECT SeatLayoutId FROM SeatLayouts WHERE Name='Default-6x40');");
            migrationBuilder.Sql("DELETE FROM `SeatLayouts` WHERE Name='Default-6x40';");
        }
    }
}
