using CountryCurrencyApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CountryCurrencyApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Country> Countries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure unique constraint on Name
            modelBuilder.Entity<Country>()
                .HasIndex(c => c.Name)
                .IsUnique();

            // Configure decimal precision
            modelBuilder.Entity<Country>()
                .Property(c => c.ExchangeRate)
                .HasPrecision(18, 6);

            modelBuilder.Entity<Country>()
                .Property(c => c.EstimatedGdp)
                .HasPrecision(18, 2);
        }
    }
}