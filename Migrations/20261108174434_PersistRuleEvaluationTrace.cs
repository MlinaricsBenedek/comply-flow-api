using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace comply_flow_api.Migrations
{
    /// <inheritdoc />
    public partial class PersistRuleEvaluationTrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvaluationTraceJson",
                table: "Rules",
                type: "varchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvaluationTraceJson",
                table: "Rules");
        }
    }
}
