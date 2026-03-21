using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoExtended.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTagSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinnedTags",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "CachedTasks");

            migrationBuilder.CreateTable(
                name: "CachedTags",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedTags", x => new { x.Name, x.TaskId });
                    table.ForeignKey(
                        name: "FK_CachedTags_CachedTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "CachedTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CachedTags_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedTags_TaskId",
                table: "CachedTags",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CachedTags_UserId_IsPinned",
                table: "CachedTags",
                columns: new[] { "UserId", "IsPinned" });

            migrationBuilder.CreateIndex(
                name: "IX_CachedTags_UserId_Name",
                table: "CachedTags",
                columns: new[] { "UserId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedTags");

            migrationBuilder.AddColumn<string>(
                name: "PinnedTags",
                table: "Users",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "CachedTasks",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);
        }
    }
}
