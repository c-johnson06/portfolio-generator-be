using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio_Generator.api.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillsToJsonUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SkillsJson",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkillsJson",
                table: "Users");
        }
    }
}
