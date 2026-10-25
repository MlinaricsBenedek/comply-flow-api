using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace comply_flow_api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchedRulePrioScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrioScore",
                table: "MatchedRules",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrioScore",
                table: "MatchedRules");
        }
    }
}
