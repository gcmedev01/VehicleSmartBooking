namespace VehicleBooking.Web.Domain.Options;

public sealed record SmtpSettings
{
    public string Server { get; init; } = "";
    public int Port { get; init; } = 25;
    public bool EnableSsl { get; init; } = true;
    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";
    public string From { get; init; } = "";
}
