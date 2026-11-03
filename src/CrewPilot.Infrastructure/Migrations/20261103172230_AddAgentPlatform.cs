using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrewPilot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalPath",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModelProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ApiKeyProtected = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Job = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ProviderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemPrompt = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: false),
                    Tools = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CommandWhitelist = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agents_ModelProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "ModelProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelCatalogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ContextLength = table.Column<int>(type: "INTEGER", nullable: true),
                    Capabilities = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    InputPricePerMillion = table.Column<decimal>(type: "TEXT", precision: 9, scale: 4, nullable: true),
                    OutputPricePerMillion = table.Column<decimal>(type: "TEXT", precision: 9, scale: 4, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCatalogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelCatalogEntries_ModelProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "ModelProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    WorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Prompt = table.Column<string>(type: "TEXT", maxLength: 32000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Output = table.Column<string>(type: "TEXT", maxLength: 32000, nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRuns_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentRuns_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RunLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 32000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunLogs_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_AgentId",
                table: "AgentRuns",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_WorkItemId",
                table: "AgentRuns",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_ProviderId",
                table: "Agents",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCatalogEntries_ProviderId_ModelId",
                table: "ModelCatalogEntries",
                columns: new[] { "ProviderId", "ModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunLogs_AgentRunId_Sequence",
                table: "RunLogs",
                columns: new[] { "AgentRunId", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelCatalogEntries");

            migrationBuilder.DropTable(
                name: "RunLogs");

            migrationBuilder.DropTable(
                name: "AgentRuns");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "ModelProviders");

            migrationBuilder.DropColumn(
                name: "LocalPath",
                table: "Projects");
        }
    }
}
