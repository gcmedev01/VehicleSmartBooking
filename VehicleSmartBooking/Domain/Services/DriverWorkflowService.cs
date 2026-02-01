using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;

public class DriverWorkflowService : IDriverWorkflowService
{
    private readonly VehicleBookingDbContext _db;

    public DriverWorkflowService(VehicleBookingDbContext db)
    {
        _db = db;
    }

    public async Task AcceptAsync(long bookingId, Driver driver)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var booking = await _db.Bookings.SingleAsync(b => b.BookingId == bookingId);

        if (booking.AssignedDriverId != driver.DriverId)
            throw new InvalidOperationException("Not your job");

        if (!TripFlowHelper.CanDriverAccept(booking))
            throw new InvalidOperationException("Invalid status");

        _db.BookingDispatchLogs.Add(new BookingDispatchLog
        {
            BookingId = booking.BookingId,
            VehicleId = booking.AssignedVehicleId!.Value,
            DriverId = driver.DriverId,
            DriverAction = DriverAction.Accepted,
            DriverActionAtUtc = DateTime.UtcNow
        });

        booking.Status = BookingStatus.DriverAccepted;
        booking.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task CompleteAsync(long bookingId, Driver driver)
    {
        var booking = await _db.Bookings.SingleAsync(b => b.BookingId == bookingId);

        if (booking.AssignedDriverId != driver.DriverId)
            throw new InvalidOperationException("Not your job");

        if (!TripFlowHelper.CanDriverComplete(booking))
            throw new InvalidOperationException("Invalid status");

        booking.Status = BookingStatus.Completed;
        booking.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task DeclineAsync(long bookingId, Driver driver, string? reason)
    {
        var booking = await _db.Bookings.SingleAsync(b => b.BookingId == bookingId);

        _db.BookingDispatchLogs.Add(new BookingDispatchLog
        {
            BookingId = booking.BookingId,
            VehicleId = booking.AssignedVehicleId!.Value,
            DriverId = driver.DriverId,
            DriverAction = DriverAction.Declined,
            DeclineReason = reason,
            DriverActionAtUtc = DateTime.UtcNow
        });

        booking.Status = BookingStatus.AdminActionRequired;
        booking.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
