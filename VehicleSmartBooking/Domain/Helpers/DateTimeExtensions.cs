namespace VehicleBooking.Web.Domain.Helpers;

public static class DateTimeExtensions
{
    // Windows: "SE Asia Standard Time"
    // Linux: "Asia/Bangkok"
    private static readonly TimeZoneInfo ThaiTz = GetThaiTimeZone();

    private static TimeZoneInfo GetThaiTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok"); }
    }

    public static DateTime ToThaiTime(this DateTime utcDateTime)
    {
        // assume stored as UTC
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, ThaiTz);
    }

    public static string ToThaiTimeString(this DateTime utcDateTime, string format = "yyyy-MM-dd HH:mm:ss")
        => utcDateTime.ToThaiTime().ToString(format);
}
