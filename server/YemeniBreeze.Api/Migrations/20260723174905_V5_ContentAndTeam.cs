using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YemeniBreeze.Api.Migrations
{
    /// <inheritdoc />
    public partial class V5_ContentAndTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentBlocks",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ValueEn = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ValueNl = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ValueAr = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBlocks", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RoleEn = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RoleNl = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RoleAr = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PhotoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    BioEn = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    BioNl = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    BioAr = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Slug = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_Slug",
                table: "TeamMembers",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentBlocks");

            migrationBuilder.DropTable(
                name: "TeamMembers");
        }
    }
}
