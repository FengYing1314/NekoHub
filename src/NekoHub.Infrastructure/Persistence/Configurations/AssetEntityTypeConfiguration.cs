using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Persistence.Configurations;

public sealed class AssetEntityTypeConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.OriginalFileName)
            .HasMaxLength(512);

        builder.Property(x => x.StoredFileName)
            .HasMaxLength(255);

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

        builder.Property(x => x.ChecksumSha256)
            .HasMaxLength(128);

        builder.Property(x => x.StorageProvider)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.StorageKey)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(x => x.PublicUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.IsPublic)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.AltText)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc);

        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
    }
}
