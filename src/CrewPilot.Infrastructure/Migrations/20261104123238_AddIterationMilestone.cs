using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrewPilot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIterationMilestone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MilestoneId",
                table: "Iterations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Iterations_MilestoneId",
                table: "Iterations",
                column: "MilestoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_Iterations_Milestones_MilestoneId",
                table: "Iterations",
                column: "MilestoneId",
                principalTable: "Milestones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Iterations_Milestones_MilestoneId",
                table: "Iterations");

            migrationBuilder.DropIndex(
                name: "IX_Iterations_MilestoneId",
                table: "Iterations");

            migrationBuilder.DropColumn(
                name: "MilestoneId",
                table: "Iterations");
        }
    }
}
