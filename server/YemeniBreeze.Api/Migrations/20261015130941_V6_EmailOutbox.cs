using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YemeniBreeze.Api.Migrations
{
    /// <inheritdoc />
    public partial class V6_EmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ManageToken",
                table: "Registrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OutgoingEmails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutgoingEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutgoingEmails_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_ManageToken",
                table: "Registrations",
                column: "ManageToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingEmails_RegistrationId_Kind",
                table: "OutgoingEmails",
                columns: new[] { "RegistrationId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingEmails_Status_NextAttemptAt",
                table: "OutgoingEmails",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutgoingEmails");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_ManageToken",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "ManageToken",
                table: "Registrations");
        }
    }
}
