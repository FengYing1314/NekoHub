using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NekoHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StoredFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Extension = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    ChecksumSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StorageProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AltText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageProviderProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProviderType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsPublicRead = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsPrivateRead = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsVisibilityToggle = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsDelete = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsDirectPublicUrl = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresAccessProxy = table.Column<bool>(type: "boolean", nullable: false),
                    RecommendedForPrimaryStorage = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlatformBacked = table.Column<bool>(type: "boolean", nullable: false),
                    IsExperimental = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresTokenForPrivateRead = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    SecretConfigurationJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageProviderProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssetDerivatives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Extension = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    StorageProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetDerivatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDerivatives_Assets_SourceAssetId",
                        column: x => x.SourceAssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetStructuredResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetStructuredResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetStructuredResults_Assets_SourceAssetId",
                        column: x => x.SourceAssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TriggerSource = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ParametersJson = table.Column<string>(type: "text", nullable: true)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "IX_AssetDerivatives_CreatedAtUtc",
                table: "AssetDerivatives",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDerivatives_SourceAssetId",
                table: "AssetDerivatives",
                column: "SourceAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDerivatives_SourceAssetId_Kind",
                table: "AssetDerivatives",
                columns: new[] { "SourceAssetId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetDerivatives_StorageKey",
                table: "AssetDerivatives",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_CreatedAtUtc",
                table: "Assets",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Status_CreatedAtUtc",
                table: "Assets",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_StorageKey",
                table: "Assets",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_StorageProviderProfileId",
                table: "Assets",
                column: "StorageProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetStructuredResults_CreatedAtUtc",
                table: "AssetStructuredResults",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AssetStructuredResults_SourceAssetId",
                table: "AssetStructuredResults",
                column: "SourceAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetStructuredResults_SourceAssetId_Kind",
                table: "AssetStructuredResults",
                columns: new[] { "SourceAssetId", "Kind" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_StorageProviderProfiles_IsDefault",
                table: "StorageProviderProfiles",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_StorageProviderProfiles_Name",
                table: "StorageProviderProfiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageProviderProfiles_ProviderType",
                table: "StorageProviderProfiles",
                column: "ProviderType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetDerivatives");

            migrationBuilder.DropTable(
                name: "AssetStructuredResults");

            migrationBuilder.DropTable(
                name: "SkillExecutionStepResults");

            migrationBuilder.DropTable(
                name: "StorageProviderProfiles");

            migrationBuilder.DropTable(
                name: "SkillExecutions");

            migrationBuilder.DropTable(
                name: "Assets");
        }
    }
}
