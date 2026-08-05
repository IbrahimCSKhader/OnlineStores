using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using onlineStore.Models;
using onlineStore.Models.CartModels;
using onlineStore.Models.Discounts;
using onlineStore.Models.Offers;
using onlineStore.Models.Identity;
using onlineStore.Models.Notifications;
using onlineStore.Models.Orders;
using onlineStore.Models.Reviews;
using onlineStore.Models.Subscriptions;
using System.Reflection;
namespace onlineStore.Data
{
    public class AppDbContext : IdentityDbContext<AppUser , AppRole , Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
           : base(options) { }
        public DbSet<Store> Stores => Set<Store>();

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Section> Sections => Set<Section>();

        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
        public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
        public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
        public DbSet<ProductVariantAttributeValue> ProductVariantAttributeValues => Set<ProductVariantAttributeValue>();
        public DbSet<StoreContactAccount> StoreContactAccounts => Set<StoreContactAccount>();
        public DbSet<StoreCustomer> StoreCustomers => Set<StoreCustomer>();
        public DbSet<StoreCustomer> CustomerStores => Set<StoreCustomer>();
        public DbSet<ShoppingCart> Carts => Set<ShoppingCart>();
        public DbSet<CartItem> CartItems => Set<CartItem>();

        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        public DbSet<Coupon> Coupons => Set<Coupon>();

        public DbSet<Review> Reviews => Set<Review>();

        public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
        public DbSet<StoreSubscription> StoreSubscriptions => Set<StoreSubscription>();
        public DbSet<Offer> Offers => Set<Offer>();
        public DbSet<OfferItem> OfferItems => Set<OfferItem>();

     
        public DbSet<Notification> Notifications => Set<Notification>();
        protected override void OnModelCreating(ModelBuilder builder)
        {

            base.OnModelCreating(builder);

            // ════════════════════════════════════════════════════
            // منع Cascade Delete على كل العلاقات الرئيسية
            // السبب: SQL Server ما بيسمح بـ multiple cascade paths
            // ════════════════════════════════════════════════════

            // Order العلاقات
            builder.Entity<Order>()
                .HasOne(o => o.Store)
                .WithMany()
                .HasForeignKey(o => o.StoreId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<Order>()
                .HasOne(o => o.StoreCustomer)
                .WithMany(sc => sc.Orders)
                .HasForeignKey(o => o.StoreCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // OrderItem العلاقات
            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cart العلاقات
            builder.Entity<ShoppingCart>()
                .HasOne(c => c.Store)
                .WithMany()
                .HasForeignKey(c => c.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ShoppingCart>()
                .HasOne(c => c.StoreCustomer)
                .WithMany(sc => sc.Carts)
                .HasForeignKey(c => c.StoreCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // CartItem العلاقات
            builder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany()
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Review العلاقات
            builder.Entity<Review>()
                .HasOne(r => r.Store)
                .WithMany()
                .HasForeignKey(r => r.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Review>()
                .HasOne(r => r.StoreCustomer)
                .WithMany(sc => sc.Reviews)
                .HasForeignKey(r => r.StoreCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Review>()
                .HasOne(r => r.Product)
                .WithMany()
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Product العلاقات
            builder.Entity<Product>()
                .HasOne(p => p.Store)
                .WithMany()
                .HasForeignKey(p => p.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Product>()
                .HasOne(p => p.Section)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProductVariant>()
                .HasOne(v => v.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProductVariantAttributeValue>()
                .HasOne(vav => vav.Variant)
                .WithMany(v => v.AttributeValues)
                .HasForeignKey(vav => vav.VariantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProductVariantAttributeValue>()
                .HasOne(vav => vav.AttributeValue)
                .WithMany(av => av.VariantValues)
                .HasForeignKey(vav => vav.AttributeValueId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProductImage>()
                .HasOne(pi => pi.Variant)
                .WithMany(v => v.Images)
                .HasForeignKey(pi => pi.VariantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Category العلاقات
            builder.Entity<Category>()
                .HasOne(c => c.Store)
                .WithMany(s => s.Categories)
                .HasForeignKey(c => c.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            // Section العلاقات
            builder.Entity<Section>()
                .HasOne(s => s.Store)
                .WithMany(st => st.Sections)
                .HasForeignKey(s => s.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification العلاقات
            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Store>()
.HasOne(s => s.Owner)
.WithMany()
.HasForeignKey(s => s.OwnerId)
.OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Store>(entity =>
            {
                entity.Property(x => x.ThemeTemplate)
                    .HasMaxLength(1)
                    .IsRequired()
                    .HasDefaultValue(StoreThemeTemplates.Default);

                entity.ToTable(t =>
                    t.HasCheckConstraint(
                        "CK_Stores_ThemeTemplate",
                        "[ThemeTemplate] IN ('D', 'L', 'F', 'P', 'B', 'U')"));
            });

            builder.Entity<StoreContactAccount>()
                .HasOne(sca => sca.Store)
                .WithMany(s => s.ContactAccounts)
                .HasForeignKey(sca => sca.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AppUser>().ToTable("Users");
            builder.Entity<AppRole>().ToTable("Roles");
            builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
            builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
            builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
            builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
            builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");


            builder.Entity<Store>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Category>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Section>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Product>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Order>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Review>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Coupon>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Notification>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<StoreContactAccount>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<SubscriptionPlan>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<StoreSubscription>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Offer>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<OfferItem>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<ProductVariantAttributeValue>().HasQueryFilter(x => !x.IsDeleted);


        
            builder.Entity<Product>(e =>
            {
                e.Property(p => p.Price).HasColumnType("decimal(18,2)");
                e.Property(p => p.CompareAtPrice).HasColumnType("decimal(18,2)");
                e.Property(p => p.CostPrice).HasColumnType("decimal(18,2)");
                e.Property(p => p.WholesalePrice)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0m);
            });

            builder.Entity<ProductVariant>(e =>
            {
                e.Property(v => v.Description).HasMaxLength(1000);
                e.Property(v => v.Price).HasColumnType("decimal(18,2)");
                e.Property(v => v.CompareAtPrice).HasColumnType("decimal(18,2)");
                e.Property(v => v.IsDefault).HasDefaultValue(false);
                e.Property(v => v.IsActive).HasDefaultValue(true);
                e.Property(v => v.SortOrder).HasDefaultValue(0);
                e.HasIndex(v => new { v.ProductId, v.SKU })
                    .IsUnique()
                    .HasFilter("[SKU] IS NOT NULL");
            });

            builder.Entity<ProductVariantAttributeValue>(e =>
            {
                e.HasIndex(vav => new { vav.VariantId, vav.AttributeValueId })
                    .IsUnique();
            });

            builder.Entity<Order>(e =>
            {
                e.Property(o => o.SubTotal).HasColumnType("decimal(18,2)");
                e.Property(o => o.DiscountAmount).HasColumnType("decimal(18,2)");
                e.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
            });

            builder.Entity<OrderItem>(e =>
            {
                e.Property(o => o.UnitPrice).HasColumnType("decimal(18,2)");
                e.Property(o => o.TotalPrice).HasColumnType("decimal(18,2)");
            });

            builder.Entity<CartItem>(e =>
            {
                e.Property(c => c.UnitPrice).HasColumnType("decimal(18,2)");
            });

            builder.Entity<Coupon>(e =>
            {
                e.Property(c => c.DiscountValue).HasColumnType("decimal(18,2)");
                e.Property(c => c.MinOrderAmount).HasColumnType("decimal(18,2)");
                e.Property(c => c.MaxDiscountAmount).HasColumnType("decimal(18,2)");
            });

            builder.Entity<SubscriptionPlan>(e =>
            {
                e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            });

            builder.Entity<StoreSubscription>(e =>
            {
                e.Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");
            });

            builder.Entity<Offer>(e =>
            {
                e.Property(x => x.BundlePrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.DiscountPercentage).HasColumnType("decimal(5,2)");
                e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
            });



            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<Order>()
                .HasOne<Coupon>(o => o.Coupon)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CouponId)
                .OnDelete(DeleteBehavior.SetNull);



            builder.Entity<Review>()
                .HasIndex(r => new { r.StoreCustomerId, r.ProductId })
                .IsUnique();

            builder.Entity<StoreCustomer>().ToTable("CustomerStores");

            builder.Entity<StoreCustomer>()
                .HasOne(cs => cs.Store)
                .WithMany()
                .HasForeignKey(cs => cs.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StoreCustomer>(e =>
            {
                e.Property(x => x.DiscountPercentage).HasColumnType("decimal(5,2)");
            });

            builder.Entity<StoreCustomer>()
                .HasQueryFilter(x => !x.IsDeleted);

            // ────────────────────────────────────────────────────
            // ⚡ PERFORMANCE: No Tracking للـ Read-Only Queries
            // السبب: بيوفر memory وبيسرّع الـ queries
            // لأن EF ما بيحتفظ بنسخة من الداتا في الـ ChangeTracker
            // استخدمه لما مش ناوي تعدّل البيانات
            // ────────────────────────────────────────────────────
            // builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            // ⬆️ تركناها كـ comment — لو فعّلتها لازم تضيف
            // AsTracking() لما بدك تعدّل بيانات


            // ────────────────────────────────────────────────────
            // ⚡ PERFORMANCE: تطبيق كل الـ Configurations
            // من ملفات منفصلة لو استخدمت IEntityTypeConfiguration
            // ────────────────────────────────────────────────────
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }


        // ════════════════════════════════════════════════════════
        // SaveChangesAsync — Auto UpdatedAt + Security
        // ════════════════════════════════════════════════════════
        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            // ────────────────────────────────────────────────────
            // ⚡ Auto UpdatedAt
            // السبب: بدل ما تكتب UpdatedAt = DateTime.UtcNow
            // في كل service، بيصير تلقائياً هون
            // UtcNow بدل Now — عشان المشروع يشتغل بأي timezone
            // ────────────────────────────────────────────────────
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Modified)
                    entry.Entity.UpdatedAt = DateTime.UtcNow;

                // ────────────────────────────────────────────────
                // 🔐 SECURITY: منع تعديل CreatedAt
                // لو حدا حاول يغير وقت الإنشاء، بنتجاهله
                // ────────────────────────────────────────────────
                if (entry.State == EntityState.Modified)
                    entry.Property(x => x.CreatedAt).IsModified = false;
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
