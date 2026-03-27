using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NekoHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetPatchBatchStatsAndSkillParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParametersJson",
                table: "SkillExecutions",
                type: MigrationColumnTypes.String(migrationBuilder),
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "Assets",
                type: MigrationColumnTypes.String(migrationBuilder, 512),
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: MigrationColumnTypes.String(migrationBuilder, 512),
                oldMaxLength: 512);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParametersJson",
                table: "SkillExecutions");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "Assets",
                type: MigrationColumnTypes.String(migrationBuilder, 512),
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: MigrationColumnTypes.String(migrationBuilder, 512),
                oldMaxLength: 512,
                oldNullable: true);
        }
    }
}
