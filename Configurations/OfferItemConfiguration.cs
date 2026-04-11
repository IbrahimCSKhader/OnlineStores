using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using onlineStore.Models.Offers;

namespace onlineStore.Configurations
{
    public class OfferItemConfiguration : IEntityTypeConfiguration<OfferItem>
    {
        public void Configure(EntityTypeBuilder<OfferItem> builder)
        {
            builder.Property(x => x.RequiredQuantity)
                .HasDefaultValue(1);

            builder.HasOne(x => x.Product)
                .WithMany(x => x.OfferItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
