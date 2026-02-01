using VehicleBooking.Web.Domain.Entities;

public interface IDriverWorkflowService
{
    Task AcceptAsync(long bookingId, Driver driver);
    Task CompleteAsync(long bookingId, Driver driver);
    Task DeclineAsync(long bookingId, Driver driver, string? reason);
}
