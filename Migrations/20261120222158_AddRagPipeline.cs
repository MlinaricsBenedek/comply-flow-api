using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace comply_flow_api.Migrations
{
    /// <inheritdoc />
    public partial class AddRagPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RagSourcesJson",
                table: "Responses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RetrievalMinSimilarity",
                table: "Configurations",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetrievalTopK",
                table: "Configurations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RagSourcesJson",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "RetrievalMinSimilarity",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "RetrievalTopK",
                table: "Configurations");
        }
    }
}
