namespace VehicleSmartBooking.Features.Dashboard.Widgets.Drivers;

public sealed class RatingDistVm
{
    public double AverageRating { get; init; }
    public int RatingCount { get; init; }
    public IReadOnlyList<RatingBucketVm> Buckets { get; init; } = Array.Empty<RatingBucketVm>();
}

public sealed class RatingBucketVm
{
    public int Score { get; init; }
    public int Count { get; init; }
}
