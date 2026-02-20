using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LP.Entity.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Viewed",
                table: "Votes",
                newName: "IsViewed");

            migrationBuilder.RenameColumn(
                name: "Reject",
                table: "Votes",
                newName: "IsReject");

            migrationBuilder.RenameColumn(
                name: "Favorite",
                table: "Votes",
                newName: "IsLike");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsViewed",
                table: "Votes",
                newName: "Viewed");

            migrationBuilder.RenameColumn(
                name: "IsReject",
                table: "Votes",
                newName: "Reject");

            migrationBuilder.RenameColumn(
                name: "IsLike",
                table: "Votes",
                newName: "Favorite");
        }
    }
}
