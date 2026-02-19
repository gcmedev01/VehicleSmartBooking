using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Data;

public sealed class VehicleBookingDbContext : DbContext
{
    public VehicleBookingDbContext(DbContextOptions<VehicleBookingDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingApproval> BookingApprovals => Set<BookingApproval>();
    public DbSet<BookingDispatchLog> BookingDispatchLogs => Set<BookingDispatchLog>();
    public DbSet<ExternalRental> ExternalRentals => Set<ExternalRental>();
    public DbSet<DriverRating> DriverRatings => Set<DriverRating>();
    public DbSet<BookingAttachment> BookingAttachments => Set<BookingAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // USERS
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users", "dbo");
            e.HasKey(x => x.UserId);

            e.Property(x => x.UserCode).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.UserCode).IsUnique().HasDatabaseName("UX_Users_UserCode");

            e.Property(x => x.UsernameTH).HasMaxLength(200).IsRequired();
            e.Property(x => x.UsernameEN).HasMaxLength(200);

            e.Property(x => x.FunctionTH).HasMaxLength(200);
            e.Property(x => x.FunctionEN).HasMaxLength(200);
            e.Property(x => x.FunctionAbbr).HasMaxLength(50);

            e.Property(x => x.DeptTH).HasMaxLength(200);
            e.Property(x => x.DeptEN).HasMaxLength(200);
            e.Property(x => x.DeptAbbr).HasMaxLength(50);

            e.Property(x => x.DivTH).HasMaxLength(200);
            e.Property(x => x.DivEN).HasMaxLength(200);
            e.Property(x => x.DivAbbr).HasMaxLength(50);

            e.Property(x => x.PositionTH).HasMaxLength(200);
            e.Property(x => x.PositionEN).HasMaxLength(200);

            e.Property(x => x.Email).HasMaxLength(256);
            // filtered unique index for Email (EF can't do filter easily in older versions; EF Core 9 supports HasFilter)
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_Users_Email").HasFilter("[Email] IS NOT NULL");

            e.Property(x => x.RoleFlags).HasDefaultValue(0);
            e.Property(x => x.IsActive).HasDefaultValue(true);

            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");
            e.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");

            // Self reference: LineManagerId
            e.HasOne(x => x.LineManager)
                .WithMany(x => x.DirectReports)
                .HasForeignKey(x => x.LineManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.DeptAbbr).HasDatabaseName("IX_Users_DeptAbbr");
        });

        // USER CREDENTIALS
        modelBuilder.Entity<UserCredential>(e =>
        {
            e.ToTable("UserCredentials", "dbo");
            e.HasKey(x => x.CredentialId);

            e.Property(x => x.LoginUsername).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.LoginUsername).IsUnique().HasDatabaseName("UX_UserCredentials_LoginUsername");

            e.Property(x => x.PasswordHash).HasColumnType("varbinary(256)").IsRequired();
            e.Property(x => x.PasswordSalt).HasColumnType("varbinary(128)").IsRequired();

            e.Property(x => x.PasswordAlgo).HasMaxLength(50).HasDefaultValue("PBKDF2-HMACSHA256");
            e.Property(x => x.Iterations).HasDefaultValue(600000);

            e.Property(x => x.IsLocked).HasDefaultValue(false);
            e.Property(x => x.FailedCount).HasDefaultValue(0);

            e.Property(x => x.LastFailedAtUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.PasswordChangedAtUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");
            e.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.User)
                .WithOne(x => x.Credential)
                .HasForeignKey<UserCredential>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("UQ_UserCredentials_User");
        });

        // VEHICLES
        modelBuilder.Entity<Vehicle>(e =>
        {
            e.ToTable("Vehicles", "dbo");
            e.HasKey(x => x.VehicleId);

            e.Property(x => x.PlateNo).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.PlateNo).IsUnique().HasDatabaseName("UX_Vehicles_PlateNo");

            e.Property(x => x.VehicleType).HasConversion<int>().IsRequired();
            e.Property(x => x.Status).HasConversion<int>().HasDefaultValue(VehicleStatus.Available);
            e.Property(x => x.IsActive).HasDefaultValue(true);

            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");
            e.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");

            e.HasIndex(x => new { x.VehicleType, x.Status }).HasDatabaseName("IX_Vehicles_TypeStatus");
        });

        // DRIVERS (1:1 Vehicle + 1:1 User)
        modelBuilder.Entity<Driver>(e =>
        {
            e.ToTable("Drivers", "dbo");
            e.HasKey(x => x.DriverId);

            e.Property(x => x.Phone).HasMaxLength(50);

            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.LastAssignedAtUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");
            e.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.User)
                .WithOne(x => x.DriverProfile)
                .HasForeignKey<Driver>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Vehicle)
                .WithOne(x => x.Driver)
                .HasForeignKey<Driver>(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("UX_Drivers_UserId");
            e.HasIndex(x => x.VehicleId).IsUnique().HasDatabaseName("UX_Drivers_VehicleId");
        });

        // BOOKINGS
        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("Bookings", "dbo");
            e.HasKey(x => x.BookingId);

            e.Property(x => x.TripType).HasConversion<int>().IsRequired();
            e.Property(x => x.VehicleTypeRequested).HasConversion<int>().IsRequired();

            e.Property(x => x.StartAtUtc).HasColumnType("datetime2(0)").IsRequired();
            e.Property(x => x.EndAtUtc).HasColumnType("datetime2(0)").IsRequired();

            e.Property(x => x.PickupLocation).HasMaxLength(500).IsRequired();
            e.Property(x => x.DestinationLocation).HasMaxLength(500).IsRequired();
            e.Property(x => x.RequesterPhone).HasMaxLength(50);
            e.Property(x => x.Purpose).HasMaxLength(500);
            e.Property(x => x.DetailNote).HasMaxLength(2000);

            e.Property(x => x.CostCenter).HasMaxLength(50);
            e.Property(x => x.JobNo).HasMaxLength(50);
            e.Property(x => x.SONo).HasMaxLength(50);

            e.Property(x => x.Status).HasConversion<int>().HasDefaultValue(BookingStatus.Draft);
            e.Property(x => x.IsExternalRental).HasDefaultValue(false);
            e.Property(x => x.IsPersonal).HasDefaultValue(false);

            e.Property(x => x.CancelledAtUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");
            e.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.Requester)
                .WithMany()
                .HasForeignKey(x => x.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.AssignedVehicle)
                .WithMany()
                .HasForeignKey(x => x.AssignedVehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.AssignedDriver)
                .WithMany()
                .HasForeignKey(x => x.AssignedDriverId)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t => t.HasCheckConstraint("CK_Bookings_Time", "[EndAtUtc] > [StartAtUtc]"));

// bookings indexes
            e.HasIndex(x => new { x.StartAtUtc, x.EndAtUtc }).HasDatabaseName("IX_Bookings_TimeRange");
            e.HasIndex(x => new { x.RequesterUserId, x.Status }).HasDatabaseName("IX_Bookings_Requester_Status");
        });

        // APPROVALS
        modelBuilder.Entity<BookingApproval>(e =>
        {
            e.ToTable("BookingApprovals", "dbo");
            e.HasKey(x => x.ApprovalId);

            e.Property(x => x.Status).HasConversion<int>().HasDefaultValue(ApprovalStatus.Pending);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.ActionAtUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.Booking)
                .WithMany(b => b.Approvals)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Approver)
                .WithMany()
                .HasForeignKey(x => x.ApproverUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.BookingId, x.LevelNo }).IsUnique().HasDatabaseName("UX_Approvals_Booking_Level");
        });

        // DISPATCH LOGS
        modelBuilder.Entity<BookingDispatchLog>(e =>
        {
            e.ToTable("BookingDispatchLogs", "dbo");
            e.HasKey(x => x.LogId);

            e.Property(x => x.DispatchedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");
            e.Property(x => x.DriverAction).HasConversion<int?>();
            e.Property(x => x.DriverActionAtUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.DeclineReason).HasMaxLength(500);

            e.HasOne(x => x.Booking)
                .WithMany(b => b.DispatchLogs)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Vehicle)
                .WithMany()
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Driver)
                .WithMany()
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.BookingId, x.AttemptNo }).IsUnique().HasDatabaseName("UX_DispatchLogs_Attempt");
        });

        // EXTERNAL RENTALS (1:1 Booking)
        modelBuilder.Entity<ExternalRental>(e =>
        {
            e.ToTable("ExternalRentals", "dbo");
            e.HasKey(x => x.ExternalRentalId);

            e.Property(x => x.VendorName).HasMaxLength(200);
            e.Property(x => x.QuotedPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.QuoteSentAtUtc).HasColumnType("datetime2(0)");

            e.Property(x => x.UserDecision).HasConversion<int>().HasDefaultValue(ExternalUserDecision.Pending);
            e.Property(x => x.UserDecisionAtUtc).HasColumnType("datetime2(0)");

            e.Property(x => x.Note).HasMaxLength(2000);

            e.Property(x => x.RentalPlateNo).HasMaxLength(50);
            e.Property(x => x.RentalDriverName).HasMaxLength(200);
            e.Property(x => x.RentalDriverPhone).HasMaxLength(50);

            e.Property(x => x.AdminClosedAtUtc).HasColumnType("datetime2(0)");

            e.HasOne(x => x.Booking)
                .WithOne(b => b.ExternalRental)
                .HasForeignKey<ExternalRental>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.BookingId).IsUnique().HasDatabaseName("UX_ExternalRentals_Booking");
        });

        // RATINGS (1:1 Booking)
        modelBuilder.Entity<DriverRating>(e =>
        {
            e.ToTable("DriverRatings", "dbo");
            e.HasKey(x => x.RatingId);

            e.Property(x => x.Score1).IsRequired();
            e.Property(x => x.Score2).IsRequired();
            e.Property(x => x.Score3).IsRequired();
            e.Property(x => x.Score4).IsRequired();
            e.Property(x => x.Score5).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Ratings_Score1", "[Score1] BETWEEN 1 AND 4");
                t.HasCheckConstraint("CK_Ratings_Score2", "[Score2] BETWEEN 1 AND 4");
                t.HasCheckConstraint("CK_Ratings_Score3", "[Score3] BETWEEN 1 AND 4");
                t.HasCheckConstraint("CK_Ratings_Score4", "[Score4] BETWEEN 1 AND 4");
                t.HasCheckConstraint("CK_Ratings_Score5", "[Score5] BETWEEN 1 AND 4");
            });

            e.HasOne(x => x.Booking)
                .WithOne(b => b.Rating)
                .HasForeignKey<DriverRating>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Driver)
                .WithMany()
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.BookingId).IsUnique().HasDatabaseName("UX_Ratings_Booking");
        });

        // ATTACHMENTS
        modelBuilder.Entity<BookingAttachment>(e =>
        {
            e.ToTable("BookingAttachments", "dbo");
            e.HasKey(x => x.AttachmentId);

            e.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.StoragePath).HasMaxLength(1000).IsRequired();

            e.Property(x => x.UploadedAtUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.Booking)
                .WithMany(b => b.Attachments)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Uploader)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.BookingId).HasDatabaseName("IX_Attachments_Booking");
        });
    }
}
