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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "Assets",
                type: "TEXT",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
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
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 512,
                oldNullable: true);
        }
    }
}
