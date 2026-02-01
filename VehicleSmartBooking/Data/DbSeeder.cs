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

        // ===== 2) Seed Driver Users (ยังไม่ใส่ Credential ตอนนี้) =====
        // สร้าง user สำหรับ driver ให้ครบตามจำนวนรถ (1 รถ : 1 คนขับ)
        // เราจะใช้ UserCode เป็น login id ของ driver (แทนชื่อที่มีเว้นวรรค)
        var now2 = DateTime.UtcNow;

        async Task<User> EnsureUserAsync(string userCode, string displayNameTh, int roleFlags)
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.UserCode == userCode);
            if (u != null) return u;

            u = new User
            {
                UserCode = userCode,
                UsernameTH = displayNameTh,
                RoleFlags = roleFlags,
                IsActive = true,
                CreatedAtUtc = now2,
                UpdatedAtUtc = now2
            };

            db.Users.Add(u);
            await db.SaveChangesAsync();
            return u;
        }

        // สมมติ RoleFlags: Driver=4, Admin=2, User=1 (คุณค่อยปรับทีหลังได้)
        const int ROLE_DRIVER = 4;

        var d1 = await EnsureUserAsync("DRV001", "พขร. 001", ROLE_DRIVER);
        var d2 = await EnsureUserAsync("DRV002", "พขร. 002", ROLE_DRIVER);
        var d3 = await EnsureUserAsync("DRV003", "พขร. 003", ROLE_DRIVER);
        var d4 = await EnsureUserAsync("DRV004", "พขร. 004", ROLE_DRIVER);

        // ===== 3) Seed Drivers (ผูก 1:1 กับ Vehicles) =====
        // map ตาม PlateNo เพื่ออ่านง่าย
        async Task EnsureDriverAsync(User user, string plateNo)
        {
            var vehicle = await db.Vehicles.FirstAsync(v => v.PlateNo == plateNo);

            var exists = await db.Drivers.AnyAsync(x => x.UserId == user.UserId || x.VehicleId == vehicle.VehicleId);
            if (exists) return;

            db.Drivers.Add(new Driver
            {
                UserId = user.UserId,
                VehicleId = vehicle.VehicleId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        await EnsureDriverAsync(d1, "กข-1111");
        await EnsureDriverAsync(d2, "กข-2222");
        await EnsureDriverAsync(d3, "กข-3333");
        await EnsureDriverAsync(d4, "กข-4444");
    }
}
