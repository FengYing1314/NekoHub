using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NekoHub.Domain.Skills;

namespace NekoHub.Infrastructure.Persistence.Configurations;

public sealed class SkillExecutionStepResultEntityTypeConfiguration : IEntityTypeConfiguration<SkillExecutionStepResult>
{
    public void Configure(EntityTypeBuilder<SkillExecutionStepResult> builder)
    {
        builder.ToTable("SkillExecutionStepResults");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.SkillExecutionId)
            .IsRequired();

        builder.Property(x => x.StepName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Succeeded)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2048);

        builder.Property(x => x.StartedAtUtc)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .IsRequired();

        builder.HasOne<SkillExecution>()
            .WithMany()
            .HasForeignKey(x => x.SkillExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SkillExecutionId);
        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => new { x.SkillExecutionId, x.StepName });
    }
}
