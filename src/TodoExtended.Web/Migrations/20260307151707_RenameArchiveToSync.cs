using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoExtended.Web.Migrations
{
    /// <inheritdoc />
    public partial class RenameArchiveToSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsArchived",
                table: "CachedTaskLists",
                newName: "IsSynced");

            migrationBuilder.RenameIndex(
                name: "IX_CachedTaskLists_IsArchived",
                table: "CachedTaskLists",
                newName: "IX_CachedTaskLists_IsSynced");

            // Invert the boolean value: IsArchived=false (active) becomes IsSynced=true (synced)
            migrationBuilder.Sql("UPDATE CachedTaskLists SET IsSynced = CASE WHEN IsSynced = 1 THEN 0 ELSE 1 END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Invert the boolean value back before renaming
            migrationBuilder.Sql("UPDATE CachedTaskLists SET IsSynced = CASE WHEN IsSynced = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "IsSynced",
                table: "CachedTaskLists",
                newName: "IsArchived");

            migrationBuilder.RenameIndex(
                name: "IX_CachedTaskLists_IsSynced",
                table: "CachedTaskLists",
                newName: "IX_CachedTaskLists_IsArchived");
        }
    }
}
