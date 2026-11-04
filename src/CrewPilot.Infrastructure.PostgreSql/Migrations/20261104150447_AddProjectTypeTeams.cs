using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrewPilot.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTypeTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefectTeamId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequirementTeamId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskTeamId",
                table: "Projects",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefectTeamId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RequirementTeamId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TaskTeamId",
                table: "Projects");
        }
    }
}
