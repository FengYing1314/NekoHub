using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NekoHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageProviderCapabilityTraitsAndGitHubProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExperimental",
                table: "StorageProviderProfiles",
                type: MigrationColumnTypes.Boolean(migrationBuilder),
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformBacked",
                table: "StorageProviderProfiles",
                type: MigrationColumnTypes.Boolean(migrationBuilder),
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExperimental",
                table: "StorageProviderProfiles");

            migrationBuilder.DropColumn(
                name: "IsPlatformBacked",
                table: "StorageProviderProfiles");
        }
    }
}
