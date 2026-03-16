using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NekoHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetDerivativesForThumbnail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetDerivatives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceAssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Extension = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    StorageProvider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    PublicUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetDerivatives");
        }
    }
}
