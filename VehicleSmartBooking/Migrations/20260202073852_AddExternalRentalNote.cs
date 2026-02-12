using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleSmartBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalRentalNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "dbo",
                table: "ExternalRentals",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                schema: "dbo",
                table: "ExternalRentals");
        }
    }
}
