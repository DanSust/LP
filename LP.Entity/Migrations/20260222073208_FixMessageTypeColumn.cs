using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LP.Entity.Migrations
{
    /// <inheritdoc />
    public partial class FixMessageTypeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WithEmail",
                table: "Profiles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WithEmail",
                table: "Profiles");
        }
    }
}
