namespace VehicleBooking.Web.Domain.Options;

public record SsoOptions
{
    public string BaseUrl { get; init; } = "";
    public string SignInPath { get; init; } = "";
    public string ValidatePath { get; init; } = "";
    public string CallbackUrl { get; init; } = "";
}
