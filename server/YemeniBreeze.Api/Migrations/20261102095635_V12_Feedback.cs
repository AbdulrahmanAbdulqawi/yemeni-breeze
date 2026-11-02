using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YemeniBreeze.Api.Migrations
{
    /// <inheritdoc />
    public partial class V12_Feedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedbackResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    Expectations = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ExpectationsOther = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    MetExpectations = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Improvements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    FavouritePart = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    JoinCommunity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Contributions = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ContributionsOther = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Dishes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RegionalFood = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Music = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Dances = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    DancesOther = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Traditions = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Missing = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Engagement = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Ideas = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AnythingElse = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackResponses_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackResponses_EventId",
                table: "FeedbackResponses",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackResponses_RegistrationId",
                table: "FeedbackResponses",
                column: "RegistrationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackResponses");
        }
    }
}
