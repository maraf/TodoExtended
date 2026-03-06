using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoExtended.Web.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTaskTemplateIdToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite doesn't support ALTER COLUMN, so rebuild the table with GUID ids.
            // 1. Create new table with Guid PK
            migrationBuilder.Sql("""
                CREATE TABLE "TaskTemplates_new" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_TaskTemplates" PRIMARY KEY,
                    "Title" TEXT NOT NULL,
                    "TaskListId" TEXT NOT NULL,
                    "TaskListName" TEXT NOT NULL,
                    "DueDateToday" INTEGER NOT NULL,
                    "ReminderTime" TEXT NULL,
                    "SortOrder" INTEGER NOT NULL
                );
                """);

            // 2. Copy existing rows, generating a UUID v4 for each
            migrationBuilder.Sql("""
                INSERT INTO "TaskTemplates_new" ("Id", "Title", "TaskListId", "TaskListName", "DueDateToday", "ReminderTime", "SortOrder")
                SELECT
                    lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab', abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))),
                    "Title", "TaskListId", "TaskListName", "DueDateToday", "ReminderTime", "SortOrder"
                FROM "TaskTemplates";
                """);

            // 3. Drop old table and rename
            migrationBuilder.Sql("""
                DROP TABLE "TaskTemplates";
                ALTER TABLE "TaskTemplates_new" RENAME TO "TaskTemplates";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE "TaskTemplates_old" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TaskTemplates" PRIMARY KEY AUTOINCREMENT,
                    "Title" TEXT NOT NULL,
                    "TaskListId" TEXT NOT NULL,
                    "TaskListName" TEXT NOT NULL,
                    "DueDateToday" INTEGER NOT NULL,
                    "ReminderTime" TEXT NULL,
                    "SortOrder" INTEGER NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "TaskTemplates_old" ("Title", "TaskListId", "TaskListName", "DueDateToday", "ReminderTime", "SortOrder")
                SELECT "Title", "TaskListId", "TaskListName", "DueDateToday", "ReminderTime", "SortOrder"
                FROM "TaskTemplates";
                """);

            migrationBuilder.Sql("""
                DROP TABLE "TaskTemplates";
                ALTER TABLE "TaskTemplates_old" RENAME TO "TaskTemplates";
                """);
        }
    }
}
