using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using ARS.Data;
using Microsoft.EntityFrameworkCore;
using System;

namespace ARS.Services
{
    /// <summary>
    /// Background service that periodically removes past schedules and cleans up flights
    /// that have no remaining schedules. Runs every configurable interval (default 15 minutes).
    /// </summary>
    public class FlightCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FlightCleanupService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);

        public FlightCleanupService(IServiceScopeFactory scopeFactory, ILogger<FlightCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FlightCleanupService started. Interval: {interval}", _interval);

            // Run once at startup then on interval
            await DoCleanupAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_interval, stoppingToken);
                    await DoCleanupAsync(stoppingToken);
                }
                catch (TaskCanceledException) { /* shutting down */ }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during flight cleanup run");
                }
            }
        }

        private async Task DoCleanupAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                var now = DateTime.Now;

                // Find schedules whose departure datetime (schedule.Date + flight.DepartureTime)
                // is strictly before now. Include Flight navigation so we can compute time.
                var expired = await db.Schedules
                    .Include(s => s.Flight)
                    .Where(s => s.Flight != null)
                    .ToListAsync(cancellationToken);

                var toRemove = new System.Collections.Generic.List<int>();
                foreach (var s in expired)
                {
                    try
                    {
                        var flight = s.Flight!;
                        var departure = s.Date.ToDateTime(TimeOnly.FromDateTime(flight.DepartureTime));
                        if (departure < now)
                        {
                            toRemove.Add(s.ScheduleID);
                        }
                    }
                    catch
                    {
                        // skip any malformed rows
                    }
                }

                if (toRemove.Count == 0)
                {
                    _logger.LogDebug("No expired schedules found.");
                }
                else
                {
                    _logger.LogInformation("Removing {count} expired schedules", toRemove.Count);

                    // Remove reservations tied to these schedules first
                    await db.Database.ExecuteSqlRawAsync($@"DELETE FROM `Reservations` WHERE `ScheduleID` IN ({string.Join(',', toRemove)})", cancellationToken);

                    // Remove schedules
                    await db.Database.ExecuteSqlRawAsync($@"DELETE FROM `Schedules` WHERE `ScheduleID` IN ({string.Join(',', toRemove)})", cancellationToken);

                    // After removing schedules, remove flights that have no schedules remaining
                    var flightsToDelete = await db.Flights
                        .Where(f => !db.Schedules.Any(s => s.FlightID == f.FlightID))
                        .Select(f => f.FlightID)
                        .ToListAsync(cancellationToken);

                    if (flightsToDelete.Count > 0)
                    {
                        _logger.LogInformation("Deleting {count} flights with no schedules", flightsToDelete.Count);
                        // delete related reservations (defensive) then delete flights
                        await db.Database.ExecuteSqlRawAsync($@"DELETE FROM `Reservations` WHERE `FlightID` IN ({string.Join(',', flightsToDelete)})", cancellationToken);
                        await db.Database.ExecuteSqlRawAsync($@"DELETE FROM `Flights` WHERE `FlightID` IN ({string.Join(',', flightsToDelete)})", cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while cleaning up expired schedules");
            }
        }
    }
}
