using Microsoft.EntityFrameworkCore;
using PortfolioGenerator.Models;

namespace PortfolioGenerator.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<SelectedRepository> SelectedRepositories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<User>()
                .HasIndex(u => u.GitHubLogin)
                .IsUnique();
                
            modelBuilder.Entity<SelectedRepository>()
                .HasIndex(sr => new { sr.UserId, sr.RepoId })
                .IsUnique();
                
            modelBuilder.Entity<SelectedRepository>()
                .Property(sr => sr.CustomBulletPoints)
                .HasConversion(
                    v => string.Join("||", v),
                    v => v.Split("||", StringSplitOptions.RemoveEmptyEntries).ToList()
                );
        }
    }
}