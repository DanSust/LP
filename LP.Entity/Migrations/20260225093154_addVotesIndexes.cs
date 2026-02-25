using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LP.Entity.Migrations
{
    /// <inheritdoc />
    public partial class addVotesIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Votes_Like_IsLike",
                table: "Votes",
                columns: new[] { "Like", "IsLike" });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_Like_IsLike_IsViewed",
                table: "Votes",
                columns: new[] { "Like", "IsLike", "IsViewed" });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_Like_IsViewed",
                table: "Votes",
                columns: new[] { "Like", "IsViewed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Votes_Like_IsLike",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_Like_IsLike_IsViewed",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_Like_IsViewed",
                table: "Votes");
        }
    }
}
