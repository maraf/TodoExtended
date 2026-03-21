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
            migrationBuilder.CreateTable(
                name: "CachedTags",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedTags", x => new { x.Name, x.UserId });
                    table.ForeignKey(
                        name: "FK_CachedTags_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CachedTaskTags",
                columns: table => new
                {
                    TagName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TagUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedTaskTags", x => new { x.TagName, x.TagUserId, x.TaskId });
                    table.ForeignKey(
                        name: "FK_CachedTaskTags_CachedTags_TagName_TagUserId",
                        columns: x => new { x.TagName, x.TagUserId },
                        principalTable: "CachedTags",
                        principalColumns: new[] { "Name", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CachedTaskTags_CachedTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "CachedTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedTags_UserId_IsPinned",
                table: "CachedTags",
                columns: new[] { "UserId", "IsPinned" });

            migrationBuilder.CreateIndex(
                name: "IX_CachedTaskTags_TagUserId_TagName",
                table: "CachedTaskTags",
                columns: new[] { "TagUserId", "TagName" });

            migrationBuilder.CreateIndex(
                name: "IX_CachedTaskTags_TaskId",
                table: "CachedTaskTags",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedTaskTags");

            migrationBuilder.DropTable(
                name: "CachedTags");
        }
    }
}
