using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NekoHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillExecutionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkillExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: MigrationColumnTypes.Guid(migrationBuilder), nullable: false),
                    SourceAssetId = table.Column<Guid>(type: MigrationColumnTypes.Guid(migrationBuilder), nullable: false),
                    SkillName = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 128), maxLength: 128, nullable: false),
                    TriggerSource = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 32), maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: false),
                    CompletedAtUtc = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: false),
                    Succeeded = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillExecutions_Assets_SourceAssetId",
                        column: x => x.SourceAssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillExecutionStepResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: MigrationColumnTypes.Guid(migrationBuilder), nullable: false),
                    SkillExecutionId = table.Column<Guid>(type: MigrationColumnTypes.Guid(migrationBuilder), nullable: false),
                    StepName = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 128), maxLength: 128, nullable: false),
                    Succeeded = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false),
                    ErrorMessage = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 2048), maxLength: 2048, nullable: true),
                    StartedAtUtc = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: false),
                    CompletedAtUtc = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillExecutionStepResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillExecutionStepResults_SkillExecutions_SkillExecutionId",
                        column: x => x.SkillExecutionId,
                        principalTable: "SkillExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkillExecutions_SourceAssetId",
                table: "SkillExecutions",
                column: "SourceAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillExecutions_SourceAssetId_StartedAtUtc",
                table: "SkillExecutions",
                columns: new[] { "SourceAssetId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SkillExecutions_StartedAtUtc",
                table: "SkillExecutions",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SkillExecutionStepResults_SkillExecutionId",
                table: "SkillExecutionStepResults",
                column: "SkillExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillExecutionStepResults_SkillExecutionId_StepName",
                table: "SkillExecutionStepResults",
                columns: new[] { "SkillExecutionId", "StepName" });

            migrationBuilder.CreateIndex(
                name: "IX_SkillExecutionStepResults_StartedAtUtc",
                table: "SkillExecutionStepResults",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkillExecutionStepResults");

            migrationBuilder.DropTable(
                name: "SkillExecutions");
        }
    }
}
