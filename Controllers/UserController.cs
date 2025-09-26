using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Octokit;
using PortfolioGenerator.Data;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserController> _logger;

    public UserController(ApplicationDbContext context, ILogger<UserController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");

            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized("Could not read access token");
            }

            var github = new GitHubClient(new ProductHeaderValue("Portfolio-Generator"))
            {
                Credentials = new Credentials(accessToken)
            };

            var githubUser = await github.User.Current();
            var githubLogin = githubUser.Login;

            // Correctly find the user by their GitHub login name
            var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.GitHubLogin == githubLogin);

            if (dbUser == null)
            {
                // If user does not exist, create a new one
                dbUser = new PortfolioGenerator.Models.User
                {
                    GitHubLogin = githubLogin,
                    Name = githubUser.Name ?? githubLogin,
                    Email = githubUser.Email ?? ""
                };
                _context.Users.Add(dbUser);
                await _context.SaveChangesAsync();
            }

            var userData = new
            {
                githubUser.Login,
                githubUser.AvatarUrl,
                dbUser.Name,
                githubUser.Location,
                githubUser.Company,
                githubUser.Bio,
                githubUser.PublicRepos,
                dbUser.Email,
                dbUser.LinkedIn,
                dbUser.Summary
            };

            return Ok(userData);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching current user data");
            // Provide a more generic error message to the client for security
            return StatusCode(500, "An internal server error occurred.");
        }
    }

    [HttpGet("repos")]
    [Authorize]
    public async Task<IActionResult> GetUserRepositories()
    {
        try
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");

            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized("Could not read access token");
            }

            var github = new GitHubClient(new ProductHeaderValue("Portfolio-Generator"))
            {
                Credentials = new Credentials(accessToken)
            };

            var repositories = await github.Repository.GetAllForCurrent(new RepositoryRequest
            {
                Type = RepositoryType.Owner,
                Sort = RepositorySort.Updated,
                Direction = SortDirection.Descending
            });

            var repoData = repositories.Select(repo => new
            {
                Id = repo.Id.ToString(),
                Name = repo.Name,
                Description = repo.Description ?? string.Empty,
                Language = repo.Language ?? string.Empty,
                StarCount = repo.StargazersCount,
                Url = repo.HtmlUrl,
                CreatedAt = repo.CreatedAt,
                UpdatedAt = repo.UpdatedAt,
                IsPrivate = repo.Private,
                Fork = repo.Fork
            }).ToList();

            return Ok(repoData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GitHub repositories");
            return StatusCode(500, "An internal server error occurred.");
        }
    }

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
        public List<string> CustomBulletPoints { get; set; } = new List<string>();
    };
}