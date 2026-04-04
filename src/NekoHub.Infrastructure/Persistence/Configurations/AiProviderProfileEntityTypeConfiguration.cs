using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NekoHub.Domain.Ai;

namespace NekoHub.Infrastructure.Persistence.Configurations;

public sealed class AiProviderProfileEntityTypeConfiguration : IEntityTypeConfiguration<AiProviderProfile>
{
    public void Configure(EntityTypeBuilder<AiProviderProfile> builder)
    {
        builder.ToTable("AiProviderProfiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ApiBaseUrl)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.ApiKey)
            .IsRequired();

        builder.Property(x => x.ApiKeyMasked)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ModelName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.DefaultSystemPrompt)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasIndex(x => x.IsActive);
    }
}
