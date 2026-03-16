using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Persistence.Configurations;

public sealed class AssetStructuredResultEntityTypeConfiguration : IEntityTypeConfiguration<AssetStructuredResult>
{
    public void Configure(EntityTypeBuilder<AssetStructuredResult> builder)
    {
        builder.ToTable("AssetStructuredResults");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.SourceAssetId)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .HasMaxLength(16384)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.SourceAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SourceAssetId);
        builder.HasIndex(x => new { x.SourceAssetId, x.Kind }).IsUnique();
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
