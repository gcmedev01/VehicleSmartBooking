using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleSmartBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingIsPersonal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPersonal",
                schema: "dbo",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPersonal",
                schema: "dbo",
                table: "Bookings");
        }
    }
}
