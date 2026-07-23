using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YemeniBreeze.Api.Migrations
{
    /// <inheritdoc />
    public partial class V4_MediaFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FolderId",
                table: "GalleryItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventId = table.Column<int>(type: "INTEGER", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaFolders_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_FolderId",
                table: "GalleryItems",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFolders_EventId",
                table: "MediaFolders",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_GalleryItems_MediaFolders_FolderId",
                table: "GalleryItems",
                column: "FolderId",
                principalTable: "MediaFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GalleryItems_MediaFolders_FolderId",
                table: "GalleryItems");

            migrationBuilder.DropTable(
                name: "MediaFolders");

            migrationBuilder.DropIndex(
                name: "IX_GalleryItems_FolderId",
                table: "GalleryItems");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "GalleryItems");
        }
    }
}
