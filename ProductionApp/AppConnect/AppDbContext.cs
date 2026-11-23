using Microsoft.EntityFrameworkCore;
using ProductionApp.Models;

namespace ProductionApp
{
    public class AppDbContext : DbContext
    {
        // Основной конструктор (для программы)
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Пустой конструктор (для консоли Add-Migration)
        public AppDbContext()
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<ProductComponent> ProductComponents { get; set; }
        public DbSet<ProductServiceWork> ProductServiceWorks { get; set; }

        // Этот метод сработает, если используется пустой конструктор
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Если настройки еще не заданы (например, при запуске из консоли), задаем строку подключения
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=ProductionDB;Trusted_Connection=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductComponent>()
                .HasOne<Product>()
                .WithMany(p => p.Components)
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductServiceWork>()
                .HasOne<Product>()
                .WithMany(p => p.ServiceWorks)
                .HasForeignKey(w => w.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}