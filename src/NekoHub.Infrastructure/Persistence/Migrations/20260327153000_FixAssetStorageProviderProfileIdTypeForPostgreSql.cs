using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NekoHub.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AssetDbContext))]
[Migration("20260327153000_FixAssetStorageProviderProfileIdTypeForPostgreSql")]
public partial class FixAssetStorageProviderProfileIdTypeForPostgreSql : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!string.Equals(
                migrationBuilder.ActiveProvider,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            return;
        }

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'Assets'
                      AND column_name = 'StorageProviderProfileId'
                      AND udt_name <> 'uuid'
                ) THEN
                    ALTER TABLE "Assets"
                    ALTER COLUMN "StorageProviderProfileId" TYPE uuid
                    USING CASE
                        WHEN "StorageProviderProfileId" IS NULL THEN NULL
                        WHEN btrim("StorageProviderProfileId") = '' THEN NULL
                        WHEN "StorageProviderProfileId" ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
                            THEN "StorageProviderProfileId"::uuid
                        ELSE NULL
                    END;
                END IF;
            END
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!string.Equals(
                migrationBuilder.ActiveProvider,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            return;
        }

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'Assets'
                      AND column_name = 'StorageProviderProfileId'
                      AND udt_name = 'uuid'
                ) THEN
                    ALTER TABLE "Assets"
                    ALTER COLUMN "StorageProviderProfileId" TYPE text
                    USING "StorageProviderProfileId"::text;
                END IF;
            END
            $$;
            """);
    }
}
