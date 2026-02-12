using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Data;

public static class DbSeeder
{
    public static async Task SeedDevAsync(VehicleBookingDbContext db)
    {
        // ทำให้ schema ล่าสุดเสมอ (dev ช่วยลด error)
        await db.Database.MigrateAsync();

        // ===== 1) Seed Vehicles =====
        if (!await db.Vehicles.AnyAsync())
        {
            var now = DateTime.UtcNow;

            db.Vehicles.AddRange(
                new Vehicle { PlateNo = "กข-1111", VehicleType = VehicleType.Sedan, Status = VehicleStatus.Available, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now },
                new Vehicle { PlateNo = "กข-2222", VehicleType = VehicleType.Sedan, Status = VehicleStatus.Available, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now },
                new Vehicle { PlateNo = "กข-3333", VehicleType = VehicleType.Van, Status = VehicleStatus.Available, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now },
                new Vehicle { PlateNo = "กข-4444", VehicleType = VehicleType.Pickup, Status = VehicleStatus.Available, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now }
            );

            await db.SaveChangesAsync();
        }
    }
}
