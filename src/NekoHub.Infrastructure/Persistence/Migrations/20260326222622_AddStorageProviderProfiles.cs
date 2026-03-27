using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NekoHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageProviderProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorageProviderProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: MigrationColumnTypes.Guid(migrationBuilder), nullable: false),
                    Name = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 128), maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 256), maxLength: 256, nullable: true),
                    ProviderType = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 64), maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false),
                    IsDefault = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false),
                    SupportsPublicRead = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false),
                    SupportsPrivateRead = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false),
                    SupportsVisibilityToggle = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false),
                    SupportsDelete = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false),
                    SupportsDirectPublicUrl = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false),
                    RequiresAccessProxy = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false),
                    RecommendedForPrimaryStorage = table.Column<bool>(type: MigrationColumnTypes.Boolean(migrationBuilder), nullable: false),
                    ConfigurationJson = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder), nullable: false),
                    SecretConfigurationJson = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder), nullable: true),
                    CreatedAtUtc = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageProviderProfiles", x => x.Id);
                });

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
                name: "StorageProviderProfiles");
        }
    }
}
