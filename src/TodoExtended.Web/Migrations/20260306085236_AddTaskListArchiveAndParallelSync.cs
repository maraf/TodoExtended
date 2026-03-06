using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoExtended.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskListArchiveAndParallelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "CachedTaskLists",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CachedTaskLists_IsArchived",
                table: "CachedTaskLists",
                column: "IsArchived");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CachedTaskLists_IsArchived",
                table: "CachedTaskLists");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "CachedTaskLists");
        }
    }
}
