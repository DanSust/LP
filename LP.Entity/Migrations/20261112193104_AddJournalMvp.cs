using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LP.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HeroImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImageLicense = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OriginalTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SourcePublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AiModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    AiGenerated = table.Column<bool>(type: "bit", nullable: false),
                    SimilarityPercent = table.Column<int>(type: "int", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EditorNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FactsChecked = table.Column<bool>(type: "bit", nullable: false),
                    AttributionChecked = table.Column<bool>(type: "bit", nullable: false),
                    ImageRightsChecked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModeratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModeratedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_Slug",
                table: "NewsItems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_Status_PublishedAt",
                table: "NewsItems",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_Status_Topic",
                table: "NewsItems",
                columns: new[] { "Status", "Topic" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsItems");
        }
    }
}
