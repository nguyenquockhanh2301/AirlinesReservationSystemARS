using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ARS.Data;
using ARS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ARS.Services
{
    public class SeatService : ISeatService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SeatService> _logger;

        public SeatService(ApplicationDbContext db, ILogger<SeatService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task GenerateFlightSeatsAsync(int scheduleId)
        {
            // If flight seats already exist for this schedule, do nothing
            if (await _db.FlightSeats.AnyAsync(fs => fs.ScheduleId == scheduleId))
            {
                _logger.LogDebug("FlightSeats already exist for schedule {ScheduleId}", scheduleId);
                return;
            }

            // Insert FlightSeats by copying Seats for the flight's seat layout
            var sql = @"
                INSERT INTO `FlightSeats` (`ScheduleId`, `SeatId`, `Status`, `Price`, `CreatedAt`)
                SELECT s.`ScheduleID`, se.`SeatId`, 0, NULL, NOW()
                FROM `Schedules` s
                JOIN `Flights` f ON s.`FlightID` = f.`FlightID`
                JOIN `Seats` se ON se.`SeatLayoutId` = f.`SeatLayoutId`
                WHERE s.`ScheduleID` = {0} AND f.`SeatLayoutId` IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM `FlightSeats` fs WHERE fs.`ScheduleId` = s.`ScheduleID` AND fs.`SeatId` = se.`SeatId`
                  );
            ";

            await _db.Database.ExecuteSqlRawAsync(sql, scheduleId);
        }

        public async Task<List<FlightSeatDto>> GetAvailableSeatsAsync(int scheduleId)
        {
            var seats = await _db.FlightSeats
                .Where(fs => fs.ScheduleId == scheduleId && fs.Status == FlightSeatStatus.Available)
                .Include(fs => fs.AircraftSeat)
                .AsNoTracking()
                .ToListAsync();

            return seats.Select(fs => new FlightSeatDto
            {
                FlightSeatId = fs.FlightSeatId,
                SeatId = fs.SeatId,
                Label = fs.AircraftSeat?.Label ?? string.Empty,
                RowNumber = fs.AircraftSeat?.RowNumber ?? 0,
                Column = fs.AircraftSeat?.Column ?? string.Empty,
                CabinClass = fs.AircraftSeat?.CabinClass ?? CabinClass.Economy,
                Price = fs.Price,
                IsAvailable = fs.Status == FlightSeatStatus.Available
            }).ToList();
        }

        public async Task<bool> ReserveSeatAsync(int flightSeatId, int reservationId)
        {
            // Attempt an atomic update to set the seat reserved only if it's available
            // Use a transaction so we can also set Reservation.FlightSeatId atomically
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var reservedValue = (int)FlightSeatStatus.Reserved;
                var updateSql = @"UPDATE `FlightSeats` SET `Status` = {0}, `ReservedByReservationID` = {1}, `UpdatedAt` = NOW() WHERE `FlightSeatId` = {2} AND `Status` = {3};";
                var affected = await _db.Database.ExecuteSqlRawAsync(updateSql, reservedValue, reservationId, flightSeatId, (int)FlightSeatStatus.Available);

                if (affected != 1)
                {
                    await tx.RollbackAsync();
                    return false;
                }

                // Now associate the reservation with the flight seat
                var reservation = await _db.Reservations.FindAsync(reservationId);
                if (reservation == null)
                {
                    // rollback the seat change
                    await _db.Database.ExecuteSqlRawAsync("UPDATE `FlightSeats` SET `Status` = {0}, `ReservedByReservationID` = NULL WHERE `FlightSeatId` = {1};", (int)FlightSeatStatus.Available, flightSeatId);
                    await tx.RollbackAsync();
                    return false;
                }

                reservation.FlightSeatId = flightSeatId;
                _db.Reservations.Update(reservation);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReserveSeatAsync failed for FlightSeat {FlightSeatId}", flightSeatId);
                try { await tx.RollbackAsync(); } catch { }
                return false;
            }
        }

        public async Task<bool> CancelReservationSeatAsync(int reservationId)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Find the flight seat reserved by this reservation
                var fs = await _db.FlightSeats.FirstOrDefaultAsync(x => x.ReservedByReservationID == reservationId);
                if (fs == null)
                {
                    // nothing to cancel
                    await tx.RollbackAsync();
                    return false;
                }

                // Clear reservation and mark available
                var updateSql = @"UPDATE `FlightSeats` SET `Status` = {0}, `ReservedByReservationID` = NULL, `UpdatedAt` = NOW() WHERE `FlightSeatId` = {1} AND `ReservedByReservationID` = {2};";
                var affected = await _db.Database.ExecuteSqlRawAsync(updateSql, (int)FlightSeatStatus.Available, fs.FlightSeatId, reservationId);
                if (affected != 1)
                {
                    await tx.RollbackAsync();
                    return false;
                }

                // Clear reservation pointer
                var reservation = await _db.Reservations.FindAsync(reservationId);
                if (reservation != null)
                {
                    reservation.FlightSeatId = null;
                    _db.Reservations.Update(reservation);
                    await _db.SaveChangesAsync();
                }

                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelReservationSeatAsync failed for Reservation {ReservationId}", reservationId);
                try { await tx.RollbackAsync(); } catch { }
                return false;
            }
        }

        // Reserve seat for a specific reservation leg
        public async Task<bool> ReserveSeatForLegAsync(int flightSeatId, int reservationLegId)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var reservedValue = (int)FlightSeatStatus.Reserved;
                var updateSql = @"UPDATE `FlightSeats` SET `Status` = {0}, `ReservedByReservationID` = {1}, `UpdatedAt` = NOW() WHERE `FlightSeatId` = {2} AND `Status` = {3};";

                // We will set ReservedByReservationID to the parent ReservationID so existing schema works.
                // First load the reservation leg to get parent reservation id
                var leg = await _db.ReservationLegs.FindAsync(reservationLegId);
                if (leg == null)
                {
                    await tx.RollbackAsync();
                    return false;
                }

                var reservationId = leg.ReservationID;

                var affected = await _db.Database.ExecuteSqlRawAsync(updateSql, reservedValue, reservationId, flightSeatId, (int)FlightSeatStatus.Available);
                if (affected != 1)
                {
                    await tx.RollbackAsync();
                    return false;
                }

                // Now set the ReservationLeg.FlightSeatId to point to the flight seat
                leg.FlightSeatId = flightSeatId;
                _db.ReservationLegs.Update(leg);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReserveSeatForLegAsync failed for FlightSeat {FlightSeatId} and Leg {ReservationLegId}", flightSeatId, reservationLegId);
                try { await tx.RollbackAsync(); } catch { }
                return false;
            }
        }

        // Cancel a reservation seat for a specific reservation leg
        public async Task<bool> CancelReservationSeatForLegAsync(int reservationLegId)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var leg = await _db.ReservationLegs.FindAsync(reservationLegId);
                if (leg == null || leg.FlightSeatId == null)
                {
                    await tx.RollbackAsync();
                    return false;
                }

                var flightSeatId = leg.FlightSeatId.Value;

                var updateSql = @"UPDATE `FlightSeats` SET `Status` = {0}, `ReservedByReservationID` = NULL, `UpdatedAt` = NOW() WHERE `FlightSeatId` = {1} AND `ReservedByReservationID` = {2};";

                var affected = await _db.Database.ExecuteSqlRawAsync(updateSql, (int)FlightSeatStatus.Available, flightSeatId, leg.ReservationID);
                if (affected != 1)
                {
                    await tx.RollbackAsync();
                    return false;
                }

                // Clear the leg pointer
                leg.FlightSeatId = null;
                _db.ReservationLegs.Update(leg);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelReservationSeatForLegAsync failed for ReservationLeg {ReservationLegId}", reservationLegId);
                try { await tx.RollbackAsync(); } catch { }
                return false;
            }
        }
    }
}
