using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NekoHub.Domain.Storage;

namespace NekoHub.Infrastructure.Persistence.Configurations;

public sealed class StorageProviderProfileEntityTypeConfiguration : IEntityTypeConfiguration<StorageProviderProfile>
{
    public void Configure(EntityTypeBuilder<StorageProviderProfile> builder)
    {
        builder.ToTable("StorageProviderProfiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(256);

        builder.Property(x => x.ProviderType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        builder.Property(x => x.IsDefault)
            .IsRequired();

        builder.Property(x => x.SupportsPublicRead)
            .IsRequired();

        builder.Property(x => x.SupportsPrivateRead)
            .IsRequired();

        builder.Property(x => x.SupportsVisibilityToggle)
            .IsRequired();

        builder.Property(x => x.SupportsDelete)
            .IsRequired();

        builder.Property(x => x.SupportsDirectPublicUrl)
            .IsRequired();

        builder.Property(x => x.RequiresAccessProxy)
            .IsRequired();

        builder.Property(x => x.RecommendedForPrimaryStorage)
            .IsRequired();

        builder.Property(x => x.IsPlatformBacked)
            .IsRequired();

        builder.Property(x => x.IsExperimental)
            .IsRequired();

        builder.Property(x => x.RequiresTokenForPrivateRead)
            .IsRequired();

        builder.Property(x => x.ConfigurationJson)
            .IsRequired();

        builder.Property(x => x.SecretConfigurationJson);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasIndex(x => x.ProviderType);

        builder.HasIndex(x => x.IsDefault);
    }
}
