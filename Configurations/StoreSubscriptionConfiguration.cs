using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using onlineStore.Models.Subscriptions;

namespace onlineStore.Configurations
{
    public class StoreSubscriptionConfiguration : IEntityTypeConfiguration<StoreSubscription>
    {
        public void Configure(EntityTypeBuilder<StoreSubscription> builder)
        {
            builder.Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");

            builder.Property(x => x.Currency)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(x => x.Notes)
                .HasMaxLength(1000);

            builder.HasOne(x => x.Store)
                .WithMany(x => x.StoreSubscriptions)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.SubscriptionPlan)
                .WithMany(x => x.StoreSubscriptions)
                .HasForeignKey(x => x.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
