using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace comply_flow_api.Migrations
{
    public partial class RemoveRuleSetVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RuleSetVersion",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "RuleSetVersion",
                table: "Rules");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RuleSetVersion",
                table: "Configurations",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RuleSetVersion",
                table: "Rules",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
