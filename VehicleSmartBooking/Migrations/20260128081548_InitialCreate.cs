using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleSmartBooking.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UsernameTH = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UsernameEN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FunctionTH = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FunctionEN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FunctionAbbr = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeptTH = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeptEN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeptAbbr = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DivTH = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DivEN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DivAbbr = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PositionTH = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PositionEN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RoleFlags = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LineManagerId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Users_Users_LineManagerId",
                        column: x => x.LineManagerId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                schema: "dbo",
                columns: table => new
                {
                    VehicleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlateNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VehicleType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.VehicleId);
                });

            migrationBuilder.CreateTable(
                name: "UserCredentials",
                schema: "dbo",
                columns: table => new
                {
                    CredentialId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<byte[]>(type: "varbinary(256)", nullable: false),
                    PasswordSalt = table.Column<byte[]>(type: "varbinary(128)", nullable: false),
                    PasswordAlgo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PBKDF2-HMACSHA256"),
                    Iterations = table.Column<int>(type: "int", nullable: false, defaultValue: 600000),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastFailedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCredentials", x => x.CredentialId);
                    table.ForeignKey(
                        name: "FK_UserCredentials_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                schema: "dbo",
                columns: table => new
                {
                    DriverId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastAssignedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.DriverId);
                    table.ForeignKey(
                        name: "FK_Drivers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Drivers_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "dbo",
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                schema: "dbo",
                columns: table => new
                {
                    BookingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequesterUserId = table.Column<int>(type: "int", nullable: false),
                    TripType = table.Column<int>(type: "int", nullable: false),
                    VehicleTypeRequested = table.Column<int>(type: "int", nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    PickupLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DestinationLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PassengerCount = table.Column<int>(type: "int", nullable: true),
                    DetailNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CostCenter = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    JobNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SONo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    AssignedVehicleId = table.Column<int>(type: "int", nullable: true),
                    AssignedDriverId = table.Column<int>(type: "int", nullable: true),
                    IsExternalRental = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.BookingId);
                    table.CheckConstraint("CK_Bookings_Time", "[EndAtUtc] > [StartAtUtc]");
                    table.ForeignKey(
                        name: "FK_Bookings_Drivers_AssignedDriverId",
                        column: x => x.AssignedDriverId,
                        principalSchema: "dbo",
                        principalTable: "Drivers",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Vehicles_AssignedVehicleId",
                        column: x => x.AssignedVehicleId,
                        principalSchema: "dbo",
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingApprovals",
                schema: "dbo",
                columns: table => new
                {
                    ApprovalId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    ApproverUserId = table.Column<int>(type: "int", nullable: false),
                    LevelNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ActionAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingApprovals", x => x.ApprovalId);
                    table.ForeignKey(
                        name: "FK_BookingApprovals_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "dbo",
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingApprovals_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingAttachments",
                schema: "dbo",
                columns: table => new
                {
                    AttachmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingAttachments", x => x.AttachmentId);
                    table.ForeignKey(
                        name: "FK_BookingAttachments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "dbo",
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingDispatchLogs",
                schema: "dbo",
                columns: table => new
                {
                    LogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    AttemptNo = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    DriverAction = table.Column<int>(type: "int", nullable: true),
                    DriverActionAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    DeclineReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingDispatchLogs", x => x.LogId);
                    table.ForeignKey(
                        name: "FK_BookingDispatchLogs_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "dbo",
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingDispatchLogs_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalSchema: "dbo",
                        principalTable: "Drivers",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingDispatchLogs_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "dbo",
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DriverRatings",
                schema: "dbo",
                columns: table => new
                {
                    RatingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverRatings", x => x.RatingId);
                    table.CheckConstraint("CK_Ratings_Score", "[Score] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_DriverRatings_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "dbo",
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DriverRatings_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalSchema: "dbo",
                        principalTable: "Drivers",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalRentals",
                schema: "dbo",
                columns: table => new
                {
                    ExternalRentalId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    QuotedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QuoteSentAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    UserDecision = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UserDecisionAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RentalPlateNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RentalDriverName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RentalDriverPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdminClosedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRentals", x => x.ExternalRentalId);
                    table.ForeignKey(
                        name: "FK_ExternalRentals_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "dbo",
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingApprovals_ApproverUserId",
                schema: "dbo",
                table: "BookingApprovals",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Approvals_Booking_Level",
                schema: "dbo",
                table: "BookingApprovals",
                columns: new[] { "BookingId", "LevelNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_Booking",
                schema: "dbo",
                table: "BookingAttachments",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAttachments_UploadedByUserId",
                schema: "dbo",
                table: "BookingAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingDispatchLogs_DriverId",
                schema: "dbo",
                table: "BookingDispatchLogs",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingDispatchLogs_VehicleId",
                schema: "dbo",
                table: "BookingDispatchLogs",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "UX_DispatchLogs_Attempt",
                schema: "dbo",
                table: "BookingDispatchLogs",
                columns: new[] { "BookingId", "AttemptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AssignedDriverId",
                schema: "dbo",
                table: "Bookings",
                column: "AssignedDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AssignedVehicleId",
                schema: "dbo",
                table: "Bookings",
                column: "AssignedVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Requester_Status",
                schema: "dbo",
                table: "Bookings",
                columns: new[] { "RequesterUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TimeRange",
                schema: "dbo",
                table: "Bookings",
                columns: new[] { "StartAtUtc", "EndAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverRatings_DriverId",
                schema: "dbo",
                table: "DriverRatings",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "UX_Ratings_Booking",
                schema: "dbo",
                table: "DriverRatings",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Drivers_UserId",
                schema: "dbo",
                table: "Drivers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Drivers_VehicleId",
                schema: "dbo",
                table: "Drivers",
                column: "VehicleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExternalRentals_Booking",
                schema: "dbo",
                table: "ExternalRentals",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_UserCredentials_User",
                schema: "dbo",
                table: "UserCredentials",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UserCredentials_LoginUsername",
                schema: "dbo",
                table: "UserCredentials",
                column: "LoginUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeptAbbr",
                schema: "dbo",
                table: "Users",
                column: "DeptAbbr");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LineManagerId",
                schema: "dbo",
                table: "Users",
                column: "LineManagerId");

            migrationBuilder.CreateIndex(
                name: "UX_Users_Email",
                schema: "dbo",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Users_UserCode",
                schema: "dbo",
                table: "Users",
                column: "UserCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TypeStatus",
                schema: "dbo",
                table: "Vehicles",
                columns: new[] { "VehicleType", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Vehicles_PlateNo",
                schema: "dbo",
                table: "Vehicles",
                column: "PlateNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingApprovals",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "BookingAttachments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "BookingDispatchLogs",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DriverRatings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ExternalRentals",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UserCredentials",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Bookings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Drivers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Vehicles",
                schema: "dbo");
        }
    }
}
