using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NekoHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetStructuredResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetStructuredResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: MigrationColumnTypes.Guid(migrationBuilder), nullable: false),
                    SourceAssetId = table.Column<Guid>(type: MigrationColumnTypes.Guid(migrationBuilder), nullable: false),
                    Kind = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 64), maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: MigrationColumnTypes.String(migrationBuilder, 16384), maxLength: 16384, nullable: false),
                    CreatedAtUtc = table.Column<long>(type: MigrationColumnTypes.Long(migrationBuilder), nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetStructuredResults");
        }
    }
}
