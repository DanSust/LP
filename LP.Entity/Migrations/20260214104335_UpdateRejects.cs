using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LP.Entity.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRejects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Rejects_Owner",
                table: "Rejects",
                column: "Owner");

            migrationBuilder.AddForeignKey(
                name: "FK_Rejects_Users_Owner",
                table: "Rejects",
                column: "Owner",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rejects_Users_Owner",
                table: "Rejects");

            migrationBuilder.DropIndex(
                name: "IX_Rejects_Owner",
                table: "Rejects");
        }
    }
}
