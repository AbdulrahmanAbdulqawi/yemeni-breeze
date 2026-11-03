using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YemeniBreeze.Api.Migrations
{
    /// <inheritdoc />
    public partial class V14_VolunteerSignups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "RegistrationId",
                table: "OutgoingEmails",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "VolunteerSignupId",
                table: "OutgoingEmails",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VolunteerForms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FieldsJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerForms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerSignups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    Contributions = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ContributionsOther = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    AnswersJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ContactedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerSignups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingEmails_VolunteerSignupId",
                table: "OutgoingEmails",
                column: "VolunteerSignupId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerSignups_Email",
                table: "VolunteerSignups",
                column: "Email");

            migrationBuilder.AddForeignKey(
                name: "FK_OutgoingEmails_VolunteerSignups_VolunteerSignupId",
                table: "OutgoingEmails",
                column: "VolunteerSignupId",
                principalTable: "VolunteerSignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OutgoingEmails_VolunteerSignups_VolunteerSignupId",
                table: "OutgoingEmails");

            migrationBuilder.DropTable(
                name: "VolunteerForms");

            migrationBuilder.DropTable(
                name: "VolunteerSignups");

            migrationBuilder.DropIndex(
                name: "IX_OutgoingEmails_VolunteerSignupId",
                table: "OutgoingEmails");

            migrationBuilder.DropColumn(
                name: "VolunteerSignupId",
                table: "OutgoingEmails");

            migrationBuilder.AlterColumn<int>(
                name: "RegistrationId",
                table: "OutgoingEmails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
