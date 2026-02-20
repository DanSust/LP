using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LP.Entity.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRejectsIndexs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Rejects_Owner_UserId",
                table: "Rejects",
                columns: new[] { "Owner", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rejects_UserId",
                table: "Rejects",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rejects_Owner_UserId",
                table: "Rejects");

            migrationBuilder.DropIndex(
                name: "IX_Rejects_UserId",
                table: "Rejects");
        }
    }
}
