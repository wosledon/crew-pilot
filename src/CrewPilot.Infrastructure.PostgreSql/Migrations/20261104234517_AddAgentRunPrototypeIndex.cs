using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrewPilot.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRunPrototypeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_PrototypeId",
                table: "AgentRuns",
                column: "PrototypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentRuns_Prototypes_PrototypeId",
                table: "AgentRuns",
                column: "PrototypeId",
                principalTable: "Prototypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentRuns_Prototypes_PrototypeId",
                table: "AgentRuns");

            migrationBuilder.DropIndex(
                name: "IX_AgentRuns_PrototypeId",
                table: "AgentRuns");
        }
    }
}
