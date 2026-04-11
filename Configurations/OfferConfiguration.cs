using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using onlineStore.Models.Offers;

namespace onlineStore.Configurations
{
    public class OfferConfiguration : IEntityTypeConfiguration<Offer>
    {
        public void Configure(EntityTypeBuilder<Offer> builder)
        {
            builder.Property(x => x.Name)
                .HasMaxLength(180)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(1000);

            builder.Property(x => x.BundlePrice).HasColumnType("decimal(18,2)");
            builder.Property(x => x.DiscountPercentage).HasColumnType("decimal(5,2)");
            builder.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");

            builder.HasOne(x => x.Store)
                .WithMany(x => x.Offers)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.Items)
                .WithOne(x => x.Offer)
                .HasForeignKey(x => x.OfferId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
