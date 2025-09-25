using System.ComponentModel.DataAnnotations;

namespace PortfolioGenerator.Models
{
    public class SelectedRepository
    {
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        public string RepoId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CustomDescription { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public int StarCount { get; set; }
        public string Url { get; set; } = string.Empty;
        public string CustomTitle { get; set; } = string.Empty;
        public List<string> CustomBulletPoints { get; set; } = new();
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        
        public User User { get; set; } = null!;
    }
}