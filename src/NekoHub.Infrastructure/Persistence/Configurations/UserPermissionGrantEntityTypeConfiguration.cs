using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NekoHub.Domain.Users;

namespace NekoHub.Infrastructure.Persistence.Configurations;

public sealed class UserPermissionGrantEntityTypeConfiguration : IEntityTypeConfiguration<UserPermissionGrant>
{
    public void Configure(EntityTypeBuilder<UserPermissionGrant> builder)
    {
        builder.ToTable("UserPermissionGrants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Permission)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.Permission);
        builder.HasIndex(x => new { x.UserId, x.Permission })
            .IsUnique();

        builder.HasOne(x => x.User)
            .WithMany(x => x.PermissionGrants)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
