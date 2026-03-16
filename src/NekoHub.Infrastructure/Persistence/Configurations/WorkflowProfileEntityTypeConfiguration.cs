using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NekoHub.Domain.Workflows;

namespace NekoHub.Infrastructure.Persistence.Configurations;

public sealed class WorkflowProfileEntityTypeConfiguration : IEntityTypeConfiguration<WorkflowProfile>
{
    public void Configure(EntityTypeBuilder<WorkflowProfile> builder)
    {
        builder.ToTable("WorkflowProfiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasMaxLength(WorkflowProfile.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(WorkflowProfile.DescriptionMaxLength);

        builder.Property(x => x.IsAutoRun)
            .IsRequired();

        builder.Property(x => x.GraphJson)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasIndex(x => x.IsAutoRun)
            .IsUnique()
            .HasFilter("\"IsAutoRun\" = TRUE");
    }
}
