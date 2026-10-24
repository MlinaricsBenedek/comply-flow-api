using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace comply_flow_api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingErrorTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "Processings",
                type: "varchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorType",
                table: "Processings",
                type: "varchar(200)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAt",
                table: "Processings",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Processings",
                type: "datetime",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "Processings");

            migrationBuilder.DropColumn(
                name: "ErrorType",
                table: "Processings");

            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "Processings");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Processings");
        }
    }
}
