using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace comply_flow_api.Migrations
{
    /// <inheritdoc />
    public partial class PersistRuleOutcomeMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EffectiveFrom",
                table: "MatchedRules",
                type: "varchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultJson",
                table: "MatchedRules",
                type: "varchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "MatchedRules",
                type: "varchar(500)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "MatchedRules");

            migrationBuilder.DropColumn(
                name: "ResultJson",
                table: "MatchedRules");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "MatchedRules");
        }
    }
}
