using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoExtended.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskReminderAndRecurring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasReminder",
                table: "CachedTasks");

            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "CachedTasks");

            migrationBuilder.AddColumn<string>(
                name: "RecurrencePattern",
                table: "CachedTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderDateTime",
                table: "CachedTasks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecurrencePattern",
                table: "CachedTasks");

            migrationBuilder.DropColumn(
                name: "ReminderDateTime",
                table: "CachedTasks");

            migrationBuilder.AddColumn<bool>(
                name: "HasReminder",
                table: "CachedTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "CachedTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
