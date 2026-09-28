using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace comply_flow_api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    RuleSetVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    GenerationMode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    TemplateVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ModelParametersJson = table.Column<string>(type: "varchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessingId = table.Column<int>(type: "int", nullable: false),
                    Metric = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    Unit = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Comment = table.Column<string>(type: "varchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchedRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<int>(type: "int", nullable: false),
                    Matched = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "varchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "varchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    ProcessingId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Processings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<int>(type: "int", nullable: false),
                    InputMessageId = table.Column<int>(type: "int", nullable: false),
                    ConfigurationId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processings_Configurations_ConfigurationId",
                        column: x => x.ConfigurationId,
                        principalTable: "Configurations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Processings_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Processings_Messages_InputMessageId",
                        column: x => x.InputMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PreProcessings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessingId = table.Column<int>(type: "int", nullable: false),
                    CleanedText = table.Column<string>(type: "varchar(max)", nullable: false),
                    CaseType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    StructuredCaseStateJson = table.Column<string>(type: "varchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreProcessings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreProcessings_Processings_ProcessingId",
                        column: x => x.ProcessingId,
                        principalTable: "Processings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResponsePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessingId = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "varchar(max)", nullable: false),
                    RequiredElementsJson = table.Column<string>(type: "varchar(max)", nullable: false),
                    ResponseStructureJson = table.Column<string>(type: "varchar(max)", nullable: false),
                    SourcesJson = table.Column<string>(type: "varchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponsePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResponsePlans_Processings_ProcessingId",
                        column: x => x.ProcessingId,
                        principalTable: "Processings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Responses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessingId = table.Column<int>(type: "int", nullable: false),
                    GenerationMode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ResponseText = table.Column<string>(type: "varchar(max)", nullable: false),
                    TemplateVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    PromptVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ModelParametersJson = table.Column<string>(type: "varchar(max)", nullable: true),
                    InputContextJson = table.Column<string>(type: "varchar(max)", nullable: true),
                    ProcessingTimeMs = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Responses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Responses_Processings_ProcessingId",
                        column: x => x.ProcessingId,
                        principalTable: "Processings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessingId = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "varchar(max)", nullable: false),
                    RuleSetVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rules_Processings_ProcessingId",
                        column: x => x.ProcessingId,
                        principalTable: "Processings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResults_ProcessingId",
                table: "EvaluationResults",
                column: "ProcessingId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedRules_RuleId",
                table: "MatchedRules",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ProcessingId",
                table: "Messages",
                column: "ProcessingId");

            migrationBuilder.CreateIndex(
                name: "IX_PreProcessings_ProcessingId",
                table: "PreProcessings",
                column: "ProcessingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processings_ConfigurationId",
                table: "Processings",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_Processings_ConversationId",
                table: "Processings",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Processings_InputMessageId",
                table: "Processings",
                column: "InputMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponsePlans_ProcessingId",
                table: "ResponsePlans",
                column: "ProcessingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Responses_ProcessingId",
                table: "Responses",
                column: "ProcessingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rules_ProcessingId",
                table: "Rules",
                column: "ProcessingId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationResults_Processings_ProcessingId",
                table: "EvaluationResults",
                column: "ProcessingId",
                principalTable: "Processings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedRules_Rules_RuleId",
                table: "MatchedRules",
                column: "RuleId",
                principalTable: "Rules",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Processings_ProcessingId",
                table: "Messages",
                column: "ProcessingId",
                principalTable: "Processings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Processings_ProcessingId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "EvaluationResults");

            migrationBuilder.DropTable(
                name: "MatchedRules");

            migrationBuilder.DropTable(
                name: "PreProcessings");

            migrationBuilder.DropTable(
                name: "ResponsePlans");

            migrationBuilder.DropTable(
                name: "Responses");

            migrationBuilder.DropTable(
                name: "Rules");

            migrationBuilder.DropTable(
                name: "Processings");

            migrationBuilder.DropTable(
                name: "Configurations");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Conversations");
        }
    }
}
