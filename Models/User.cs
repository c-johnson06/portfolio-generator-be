using System.ComponentModel.DataAnnotations;

namespace PortfolioGenerator.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string LinkedIn { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public string GitHubLogin { get; set; } = string.Empty;

        public List<SelectedRepository> SelectedRepositories { get; set; } = new();
        
    }
}