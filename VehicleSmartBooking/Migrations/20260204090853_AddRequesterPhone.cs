using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleSmartBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddRequesterPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequesterPhone",
                schema: "dbo",
                table: "Bookings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequesterPhone",
                schema: "dbo",
                table: "Bookings");
        }
    }
}
