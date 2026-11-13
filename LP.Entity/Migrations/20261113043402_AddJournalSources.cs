using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LP.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "NewsItems",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "NewsItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JournalSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DefaultTags = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    SyncIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxItemsPerSync = table.Column<int>(type: "int", nullable: false),
                    IncludePattern = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AiDrafts = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastSyncError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalSources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_SourceId_ExternalId",
                table: "NewsItems",
                columns: new[] { "SourceId", "ExternalId" },
                unique: true,
                filter: "[SourceId] IS NOT NULL AND [ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalSources_IsEnabled",
                table: "JournalSources",
                column: "IsEnabled");

            migrationBuilder.AddForeignKey(
                name: "FK_NewsItems_JournalSources_SourceId",
                table: "NewsItems",
                column: "SourceId",
                principalTable: "JournalSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NewsItems_JournalSources_SourceId",
                table: "NewsItems");

            migrationBuilder.DropTable(
                name: "JournalSources");

            migrationBuilder.DropIndex(
                name: "IX_NewsItems_SourceId_ExternalId",
                table: "NewsItems");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "NewsItems");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "NewsItems");
        }
    }
}
