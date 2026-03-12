using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoExtended.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserScopingToAllEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CachedTasks_IsDeleted_DueDate",
                table: "CachedTasks");

            migrationBuilder.DropIndex(
                name: "IX_CachedTaskLists_IsSynced",
                table: "CachedTaskLists");

            // Add UserId columns with empty default (temporary for migration)
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "TaskTemplates",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "SyncMetadata",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "CachedTasks",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "CachedTaskLists",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            // Assign orphaned rows to the single existing user (if one exists)
            migrationBuilder.Sql("""
                UPDATE TaskTemplates SET UserId = (SELECT Id FROM Users LIMIT 1) WHERE UserId = '' AND (SELECT COUNT(*) FROM Users) = 1;
                UPDATE CachedTasks SET UserId = (SELECT Id FROM Users LIMIT 1) WHERE UserId = '' AND (SELECT COUNT(*) FROM Users) = 1;
                UPDATE CachedTaskLists SET UserId = (SELECT Id FROM Users LIMIT 1) WHERE UserId = '' AND (SELECT COUNT(*) FROM Users) = 1;
                UPDATE SyncMetadata SET UserId = (SELECT Id FROM Users LIMIT 1) WHERE UserId IS NULL AND (SELECT COUNT(*) FROM Users) = 1;
                """);

            // Rename global delta token key to per-user format for the existing user
            migrationBuilder.Sql("""
                UPDATE SyncMetadata SET Key = 'TaskListsDeltaToken:' || (SELECT Id FROM Users LIMIT 1) WHERE Key = 'TaskListsDeltaToken' AND (SELECT COUNT(*) FROM Users) = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_UserId",
                table: "TaskTemplates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CachedTasks_UserId_IsDeleted_DueDate",
                table: "CachedTasks",
                columns: new[] { "UserId", "IsDeleted", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CachedTaskLists_UserId_IsSynced",
                table: "CachedTaskLists",
                columns: new[] { "UserId", "IsSynced" });

            migrationBuilder.AddForeignKey(
                name: "FK_TaskTemplates_Users_UserId",
                table: "TaskTemplates",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskTemplates_Users_UserId",
                table: "TaskTemplates");

            migrationBuilder.DropIndex(
                name: "IX_TaskTemplates_UserId",
                table: "TaskTemplates");

            migrationBuilder.DropIndex(
                name: "IX_CachedTasks_UserId_IsDeleted_DueDate",
                table: "CachedTasks");

            migrationBuilder.DropIndex(
                name: "IX_CachedTaskLists_UserId_IsSynced",
                table: "CachedTaskLists");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "TaskTemplates");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SyncMetadata");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CachedTasks");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CachedTaskLists");

            migrationBuilder.CreateIndex(
                name: "IX_CachedTasks_IsDeleted_DueDate",
                table: "CachedTasks",
                columns: new[] { "IsDeleted", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CachedTaskLists_IsSynced",
                table: "CachedTaskLists",
                column: "IsSynced");
        }
    }
}
