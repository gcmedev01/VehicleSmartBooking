namespace VehicleBooking.Web.Domain.Options;

public sealed record EmailNotificationOptions
{
    public string BaseUrl { get; init; } = "";
    public bool TestMode { get; init; }
    public string TestRecipient { get; init; } = "";
}
