namespace PortfolioGenerator.Models.DTOs;

public class ResumeDataRequest
{
    public string Email { get; set; } = string.Empty;
    public string LinkedIn { get; set; } = string.Empty;
    public string ProfessionalSummary { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public List<SelectedRepoRequest> SelectedRepositories { get; set; } = new();
}

public class SelectedRepoRequest
{
    public string RepoId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CustomDescription { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int StarCount { get; set; }
    public string Url { get; set; } = string.Empty;
    public string CustomTitle { get; set; } = string.Empty;
    public List<string>? CustomBulletPoints { get; set; }
}

public class ResumeDataResponse
{
    public UserInfo User { get; set; } = new();
    public List<SelectedRepoInfo> SelectedRepositories { get; set; } = new();
}

public class UserInfo
{
    public string GitHubLogin { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LinkedIn { get; set; } = string.Empty;
    public string ProfessionalSummary { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
}

public class SelectedRepoInfo
{
    public string RepoId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CustomDescription { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int StarCount { get; set; }
    public string Url { get; set; } = string.Empty;
    public string CustomTitle { get; set; } = string.Empty;
    public List<string> CustomBulletPoints { get; set; } = new();
    public string AddedAt { get; set; } = string.Empty; // Keep as string for the PDF template
}