using System.ComponentModel.DataAnnotations;
using VehicleBooking.Web.Domain.Entities;

namespace VehicleSmartBooking.Models;

public sealed class BookingRatingViewModel : IValidatableObject
{
    public Booking? Booking { get; set; }

    [Range(1, 4)]
    public int? Score1 { get; set; }

    [Range(1, 4)]
    public int? Score2 { get; set; }

    [Range(1, 4)]
    public int? Score3 { get; set; }

    [Range(1, 4)]
    public int? Score4 { get; set; }

    [Range(1, 4)]
    public int? Score5 { get; set; }

    public string? Comment { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Score1.HasValue || !Score2.HasValue || !Score3.HasValue || !Score4.HasValue || !Score5.HasValue)
        {
            yield return new ValidationResult("กรุณาให้คะแนนคนขับให้ครบทุกข้อ");
        }
    }
}
