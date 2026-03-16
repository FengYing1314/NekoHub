using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Persistence.Configurations;

public sealed class AssetDerivativeEntityTypeConfiguration : IEntityTypeConfiguration<AssetDerivative>
{
    public void Configure(EntityTypeBuilder<AssetDerivative> builder)
    {
        builder.ToTable("AssetDerivatives");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.SourceAssetId)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Extension)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Size)
            .IsRequired();

        builder.Property(x => x.Width);

        builder.Property(x => x.Height);

        builder.Property(x => x.StorageProvider)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.StorageKey)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(x => x.PublicUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.SourceAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SourceAssetId);
        builder.HasIndex(x => new { x.SourceAssetId, x.Kind }).IsUnique();
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.StorageKey).IsUnique();
    }
}
