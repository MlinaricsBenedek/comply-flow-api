using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace comply_flow_api.Migrations
{
    /// <inheritdoc />
    public partial class BankAwareRuleSetsAndRemoveMatchedRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchedRules");

            migrationBuilder.AddColumn<string>(
                name: "Bank",
                table: "Rules",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "otp");

            migrationBuilder.AddColumn<string>(
                name: "Bank",
                table: "Configurations",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "otp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bank",
                table: "Rules");

            migrationBuilder.DropColumn(
                name: "Bank",
                table: "Configurations");

            migrationBuilder.CreateTable(
                name: "MatchedRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<string>(type: "varchar(50)", nullable: true),
                    Matched = table.Column<bool>(type: "bit", nullable: false),
                    PrioScore = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(max)", nullable: false),
                    ResultJson = table.Column<string>(type: "varchar(max)", nullable: false),
                    Source = table.Column<string>(type: "varchar(500)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchedRules_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedRules_RuleId",
                table: "MatchedRules",
                column: "RuleId");
        }
    }
}
