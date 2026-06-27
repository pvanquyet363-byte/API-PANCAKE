using API_PanCake.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace API_PanCake.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<WebhookOrderEntity> WebhookOrders { get; set; } = null!;
        
        public DbSet<WebhookCustomerEntity> WebhookCustomers { get; set; } = null!;
        
        public DbSet<DmkbApiEntity> DmkbApis { get; set; } = null!;

        public DbSet<PanCakeOrderMappingEntity> PanCakeOrderMappings { get; set; } = null!;
        
        public DbSet<InventorySyncQueueEntity> InventorySyncQueue { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<WebhookOrderEntity>(entity =>
            {
                entity.HasIndex(e => e.PanCakeOrderId);
                entity.HasIndex(e => e.ShopId);
                entity.HasIndex(e => e.EventType);
                entity.HasIndex(e => e.WebhookReceivedAt);
                entity.HasIndex(e => e.IsProcessed);

                entity.Property(e => e.WebhookReceivedAt)
                    .HasDefaultValueSql("GETDATE()");
                
                entity.Property(e => e.IsProcessed)
                    .HasDefaultValue(false);
            });

            modelBuilder.Entity<WebhookCustomerEntity>(entity =>
            {
                entity.HasIndex(e => e.PanCakeCustomerId);
                entity.HasIndex(e => e.ShopId);
                entity.HasIndex(e => e.EventType);
                entity.HasIndex(e => e.WebhookReceivedAt);
                entity.HasIndex(e => e.IsProcessed);
                entity.HasIndex(e => e.CustomerPhone);
                entity.HasIndex(e => e.CustomerEmail);

                entity.Property(e => e.WebhookReceivedAt)
                    .HasDefaultValueSql("GETDATE()");
                
                entity.Property(e => e.IsProcessed)
                    .HasDefaultValue(false);
            });

            modelBuilder.Entity<PanCakeOrderMappingEntity>(entity =>
            {
                // Index cho query nhanh
                entity.HasIndex(e => new { e.PanCakeOrderId, e.ShopId })
                    .IsUnique() // Mỗi đơn PanCake chỉ map 1 lần
                    .HasDatabaseName("IX_PanCakeOrderMapping_Order_Shop");

                entity.HasIndex(e => e.SttRec)
                    .HasDatabaseName("IX_PanCakeOrderMapping_SttRec");

                entity.HasIndex(e => e.FirstSyncedAt)
                    .HasDatabaseName("IX_PanCakeOrderMapping_FirstSyncedAt");

                entity.Property(e => e.FirstSyncedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(e => e.SyncCount)
                    .HasDefaultValue(1);
            });
        }
    }
}
