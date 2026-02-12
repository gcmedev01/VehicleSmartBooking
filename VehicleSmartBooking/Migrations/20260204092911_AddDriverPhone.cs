using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleSmartBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "dbo",
                table: "Drivers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "dbo",
                table: "Drivers");
        }
    }
}
