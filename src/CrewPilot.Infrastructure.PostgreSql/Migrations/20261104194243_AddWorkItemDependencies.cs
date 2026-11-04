using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrewPilot.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItemDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromWorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToWorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemDependencies_WorkItems_FromWorkItemId",
                        column: x => x.FromWorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemDependencies_WorkItems_ToWorkItemId",
                        column: x => x.ToWorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemDependencies_FromWorkItemId_ToWorkItemId",
                table: "WorkItemDependencies",
                columns: new[] { "FromWorkItemId", "ToWorkItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemDependencies_ProjectId",
                table: "WorkItemDependencies",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemDependencies_ToWorkItemId",
                table: "WorkItemDependencies",
                column: "ToWorkItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemDependencies");
        }
    }
}
