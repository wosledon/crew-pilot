using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrewPilot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowsAndBlockers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowRunId",
                table: "AgentRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStepId",
                table: "AgentRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Goal = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentTeams_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsTemplate = table.Column<bool>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workflows_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgentTeamMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentTeamMembers_AgentTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "AgentTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentTeamMembers_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CurrentStepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentStepId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowRuns_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowRuns_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RequiredJob = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Instruction = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    ApprovalPolicy = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    MaxRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    InputContract = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OutputContract = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowSteps_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkflowSteps_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Blockers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AgentRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ContextSnapshot = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    ResolutionNote = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blockers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Blockers_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Blockers_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Blockers_WorkflowRuns_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalTable: "WorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_WorkflowRunId",
                table: "AgentRuns",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_WorkflowStepId",
                table: "AgentRuns",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTeamMembers_AgentId",
                table: "AgentTeamMembers",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTeamMembers_TeamId_AgentId",
                table: "AgentTeamMembers",
                columns: new[] { "TeamId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentTeams_ProjectId",
                table: "AgentTeams",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Blockers_AgentRunId",
                table: "Blockers",
                column: "AgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Blockers_ProjectId_Status",
                table: "Blockers",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Blockers_WorkflowRunId",
                table: "Blockers",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Blockers_WorkItemId",
                table: "Blockers",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_WorkflowId",
                table: "WorkflowRuns",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_WorkItemId",
                table: "WorkflowRuns",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_ProjectId",
                table: "Workflows",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_AgentId",
                table: "WorkflowSteps",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowId_Order",
                table: "WorkflowSteps",
                columns: new[] { "WorkflowId", "Order" });

            migrationBuilder.AddForeignKey(
                name: "FK_AgentRuns_WorkflowRuns_WorkflowRunId",
                table: "AgentRuns",
                column: "WorkflowRunId",
                principalTable: "WorkflowRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentRuns_WorkflowSteps_WorkflowStepId",
                table: "AgentRuns",
                column: "WorkflowStepId",
                principalTable: "WorkflowSteps",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentRuns_WorkflowRuns_WorkflowRunId",
                table: "AgentRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_AgentRuns_WorkflowSteps_WorkflowStepId",
                table: "AgentRuns");

            migrationBuilder.DropTable(
                name: "AgentTeamMembers");

            migrationBuilder.DropTable(
                name: "Blockers");

            migrationBuilder.DropTable(
                name: "WorkflowSteps");

            migrationBuilder.DropTable(
                name: "AgentTeams");

            migrationBuilder.DropTable(
                name: "WorkflowRuns");

            migrationBuilder.DropTable(
                name: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_AgentRuns_WorkflowRunId",
                table: "AgentRuns");

            migrationBuilder.DropIndex(
                name: "IX_AgentRuns_WorkflowStepId",
                table: "AgentRuns");

            migrationBuilder.DropColumn(
                name: "WorkflowRunId",
                table: "AgentRuns");

            migrationBuilder.DropColumn(
                name: "WorkflowStepId",
                table: "AgentRuns");
        }
    }
}
