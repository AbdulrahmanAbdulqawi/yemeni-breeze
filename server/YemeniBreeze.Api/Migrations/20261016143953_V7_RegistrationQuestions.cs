using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YemeniBreeze.Api.Migrations
{
    /// <inheritdoc />
    public partial class V7_RegistrationQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Registrations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DietaryRestrictions",
                table: "Registrations",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestNames",
                table: "Registrations",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVegetarian",
                table: "Registrations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Registrations",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WantsUpdates",
                table: "Registrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "DietaryRestrictions",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "GuestNames",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "IsVegetarian",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "WantsUpdates",
                table: "Registrations");
        }
    }
}
