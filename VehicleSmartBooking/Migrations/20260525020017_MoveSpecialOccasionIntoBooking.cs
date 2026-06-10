using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleSmartBooking.Migrations
{
    /// <inheritdoc />
    public partial class MoveSpecialOccasionIntoBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpecialOccasionType",
                schema: "dbo",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialOccasionRemark",
                schema: "dbo",
                table: "Bookings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE b
                SET
                    b.SpecialOccasionType = s.OccasionType,
                    b.SpecialOccasionRemark = s.Remark
                FROM dbo.Bookings b
                INNER JOIN dbo.BookingSpecialOccasions s
                    ON s.BookingId = b.BookingId
            ");

            migrationBuilder.DropTable(
                name: "BookingSpecialOccasions",
                schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingSpecialOccasions",
                schema: "dbo",
                columns: table => new
                {
                    BookingSpecialOccasionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    OccasionType = table.Column<int>(type: "int", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSpecialOccasions", x => x.BookingSpecialOccasionId);
                    table.ForeignKey(
                        name: "FK_BookingSpecialOccasions_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "dbo",
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_BookingSpecialOccasions_Booking",
                schema: "dbo",
                table: "BookingSpecialOccasions",
                column: "BookingId",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO dbo.BookingSpecialOccasions (BookingId, OccasionType, Remark, CreatedAtUtc)
                SELECT
                    BookingId,
                    SpecialOccasionType,
                    SpecialOccasionRemark,
                    sysutcdatetime()
                FROM dbo.Bookings
                WHERE SpecialOccasionType IS NOT NULL
            ");

            migrationBuilder.DropColumn(
                name: "SpecialOccasionType",
                schema: "dbo",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SpecialOccasionRemark",
                schema: "dbo",
                table: "Bookings");
        }
    }
}
