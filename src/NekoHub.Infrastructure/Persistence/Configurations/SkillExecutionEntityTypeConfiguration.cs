using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NekoHub.Domain.Assets;
using NekoHub.Domain.Skills;

namespace NekoHub.Infrastructure.Persistence.Configurations;

public sealed class SkillExecutionEntityTypeConfiguration : IEntityTypeConfiguration<SkillExecution>
{
    public void Configure(EntityTypeBuilder<SkillExecution> builder)
    {
        builder.ToTable("SkillExecutions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.SourceAssetId)
            .IsRequired();

        builder.Property(x => x.SkillName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.TriggerSource)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.StartedAtUtc)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .IsRequired();

        builder.Property(x => x.Succeeded)
            .IsRequired();

        builder.Property(x => x.ParametersJson);

        builder.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.SourceAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SourceAssetId);
        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => new { x.SourceAssetId, x.StartedAtUtc });
    }
}
