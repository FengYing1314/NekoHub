using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NekoHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAssetPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: MigrationColumnTypes.Guid(migrationBuilder), nullable: false),
                    Type = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 32), maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 32), maxLength: 32, nullable: false),
                    OriginalFileName = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 512), maxLength: 512, nullable: false),
                    StoredFileName = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 255), maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 255), maxLength: 255, nullable: false),
                    Extension = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 32), maxLength: 32, nullable: false),
                    Size = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: false),
                    Width = table.Column<int>(type: MigrationColumnTypes.Int(migrationBuilder), nullable: true),
                    Height = table.Column<int>(type: MigrationColumnTypes.Int(migrationBuilder), nullable: true),
                    ChecksumSha256 = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 128), maxLength: 128, nullable: true),
                    StorageProvider = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 64), maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 1024), maxLength: 1024, nullable: false),
                    PublicUrl = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 2048), maxLength: 2048, nullable: true),
                    Description = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 1000), maxLength: 1000, nullable: true),
                    AltText = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 1000), maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: false),
                    DeletedAtUtc = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assets");
        }
    }
}
