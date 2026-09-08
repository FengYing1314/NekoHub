using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Persistence.Configurations;

public sealed class AssetProcessingJobEntityTypeConfiguration : IEntityTypeConfiguration<AssetProcessingJob>
{
    public void Configure(EntityTypeBuilder<AssetProcessingJob> builder)
    {
        builder.ToTable("AssetProcessingJobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasMaxLength(32);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb");
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Status, x.AvailableAtUtc });
        builder.HasIndex(x => x.AssetId);
        builder.HasIndex(x => x.StorageProviderProfileId);
        // 删除资产后仍须保留文件清理任务，因此不设置级联删除外键。
    }
}
