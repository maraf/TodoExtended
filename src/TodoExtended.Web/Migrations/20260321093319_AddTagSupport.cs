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
            migrationBuilder.DropForeignKey(
                name: "FK_CachedTags_CachedTasks_TaskId",
                table: "CachedTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CachedTags",
                table: "CachedTags");

            migrationBuilder.DropIndex(
                name: "IX_CachedTags_TaskId",
                table: "CachedTags");

            migrationBuilder.DropIndex(
                name: "IX_CachedTags_UserId_Name",
                table: "CachedTags");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "CachedTags");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CachedTags",
                table: "CachedTags",
                columns: new[] { "Name", "UserId" });

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

            migrationBuilder.DropPrimaryKey(
                name: "PK_CachedTags",
                table: "CachedTags");

            migrationBuilder.AddColumn<string>(
                name: "TaskId",
                table: "CachedTags",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CachedTags",
                table: "CachedTags",
                columns: new[] { "Name", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_CachedTags_TaskId",
                table: "CachedTags",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CachedTags_UserId_Name",
                table: "CachedTags",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_CachedTags_CachedTasks_TaskId",
                table: "CachedTags",
                column: "TaskId",
                principalTable: "CachedTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
