using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using onlineStore.Models.Orders;

namespace onlineStore.Configurations;

public class OrderInvoicePrintConfiguration : IEntityTypeConfiguration<OrderInvoicePrint>
{
    public void Configure(EntityTypeBuilder<OrderInvoicePrint> builder)
    {
        builder.ToTable("OrderInvoicePrints");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.OrderId, x.CreatedAt });
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
