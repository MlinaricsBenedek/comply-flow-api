using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace comply_flow_api.Migrations
{
    public partial class RemoveActionAndNextStepFromResponsePlan : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Action",
                table: "ResponsePlans");

            migrationBuilder.DropColumn(
                name: "NextStep",
                table: "ResponsePlans");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "ResponsePlans",
                type: "varchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NextStep",
                table: "ResponsePlans",
                type: "varchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
