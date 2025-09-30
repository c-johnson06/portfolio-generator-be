using Microsoft.EntityFrameworkCore;
using PortfolioGenerator.Models;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
                )
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));
        }
    }
}