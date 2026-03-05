using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoExtended.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistentTokenCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HomeAccountId",
                table: "Users",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DistributedCacheEntries",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Value = table.Column<byte[]>(type: "BLOB", nullable: false),
                    AbsoluteExpiration = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SlidingExpirationInSeconds = table.Column<double>(type: "REAL", nullable: true),
                    LastAccessed = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributedCacheEntries", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistributedCacheEntries_AbsoluteExpiration",
                table: "DistributedCacheEntries",
                column: "AbsoluteExpiration");

            migrationBuilder.CreateIndex(
                name: "IX_DistributedCacheEntries_LastAccessed",
                table: "DistributedCacheEntries",
                column: "LastAccessed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistributedCacheEntries");

            migrationBuilder.DropColumn(
                name: "HomeAccountId",
                table: "Users");
        }
    }
}
