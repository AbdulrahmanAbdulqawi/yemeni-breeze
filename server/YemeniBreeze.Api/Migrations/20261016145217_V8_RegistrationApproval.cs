using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YemeniBreeze.Api.Migrations
{
    /// <inheritdoc />
    public partial class V8_RegistrationApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                table: "Events");
        }
    }
}
