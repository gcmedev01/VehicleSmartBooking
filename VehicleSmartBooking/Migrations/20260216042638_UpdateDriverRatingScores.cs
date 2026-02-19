using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleSmartBooking.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDriverRatingScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Ratings_Score",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.DropColumn(
                name: "Score",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.AddColumn<int>(
                name: "Score1",
                schema: "dbo",
                table: "DriverRatings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Score2",
                schema: "dbo",
                table: "DriverRatings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Score3",
                schema: "dbo",
                table: "DriverRatings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Score4",
                schema: "dbo",
                table: "DriverRatings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Score5",
                schema: "dbo",
                table: "DriverRatings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ratings_Score1",
                schema: "dbo",
                table: "DriverRatings",
                sql: "[Score1] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ratings_Score2",
                schema: "dbo",
                table: "DriverRatings",
                sql: "[Score2] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ratings_Score3",
                schema: "dbo",
                table: "DriverRatings",
                sql: "[Score3] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ratings_Score4",
                schema: "dbo",
                table: "DriverRatings",
                sql: "[Score4] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ratings_Score5",
                schema: "dbo",
                table: "DriverRatings",
                sql: "[Score5] BETWEEN 1 AND 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Ratings_Score1",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ratings_Score2",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ratings_Score3",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ratings_Score4",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ratings_Score5",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.DropColumn(
                name: "Score1",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.DropColumn(
                name: "Score2",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.DropColumn(
                name: "Score3",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.DropColumn(
                name: "Score4",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.DropColumn(
                name: "Score5",
                schema: "dbo",
                table: "DriverRatings");

            migrationBuilder.AddColumn<int>(
                name: "Score",
                schema: "dbo",
                table: "DriverRatings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ratings_Score",
                schema: "dbo",
                table: "DriverRatings",
                sql: "[Score] BETWEEN 1 AND 5");
        }
    }
}
