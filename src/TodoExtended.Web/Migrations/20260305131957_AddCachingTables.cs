using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoExtended.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCachingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedTaskLists",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DeltaToken = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    LastSyncUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedTaskLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncMetadata",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncMetadata", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "CachedTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ListId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Importance = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CachedTasks_CachedTaskLists_ListId",
                        column: x => x.ListId,
                        principalTable: "CachedTaskLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedTaskLists_LastSyncUtc",
                table: "CachedTaskLists",
                column: "LastSyncUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CachedTasks_IsDeleted_DueDate",
                table: "CachedTasks",
                columns: new[] { "IsDeleted", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CachedTasks_ListId",
                table: "CachedTasks",
                column: "ListId");

            migrationBuilder.CreateIndex(
                name: "IX_CachedTasks_ListId_IsDeleted",
                table: "CachedTasks",
                columns: new[] { "ListId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedTasks");

            migrationBuilder.DropTable(
                name: "SyncMetadata");

            migrationBuilder.DropTable(
                name: "CachedTaskLists");
        }
    }
}
