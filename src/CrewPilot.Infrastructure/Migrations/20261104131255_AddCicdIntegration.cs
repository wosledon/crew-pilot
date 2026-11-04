using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrewPilot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCicdIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CiStatus",
                table: "WorkItems",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrState",
                table: "WorkItems",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TokenProtected = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Repository = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectIntegrations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIntegrations_ProjectId",
                table: "ProjectIntegrations",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectIntegrations");

            migrationBuilder.DropColumn(
                name: "CiStatus",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "PrState",
                table: "WorkItems");
        }
    }
}
