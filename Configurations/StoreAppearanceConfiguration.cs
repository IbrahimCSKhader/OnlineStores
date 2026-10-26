using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using onlineStore.Models;

namespace onlineStore.Configurations;

public class StoreAppearanceConfiguration : IEntityTypeConfiguration<StoreAppearance>
{
    public void Configure(EntityTypeBuilder<StoreAppearance> builder)
    {
        builder.HasKey(x => x.StoreId);
        builder.Property(x => x.OverridesJson).HasMaxLength(16000).IsRequired();
        builder.HasOne(x => x.Store).WithOne(x => x.Appearance)
            .HasForeignKey<StoreAppearance>(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
    }
}
