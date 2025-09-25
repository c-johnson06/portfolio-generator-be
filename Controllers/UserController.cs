using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Octokit;
using Microsoft.AspNetCore.Authentication.Cookies;
using PortfolioGenerator.Data;
using PortfolioGenerator.Models;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UserController(ApplicationDbContext context)
    {
        _context = context;
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

            var user = await github.User.Current();
            var userData = new
            {
                user.Login,
                user.AvatarUrl,
                user.Name,
                user.Location,
                user.Company,
                user.Bio,
                user.PublicRepos,
                user.Email
            };

            return Ok(userData);

        }
        catch (AuthorizationException)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized("Invalid access token");
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Unauthorized("GitHub token expired or invalid");
            }
            return StatusCode(500, "Error fetching GitHub user data");
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal server error");
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
        catch (AuthorizationException)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized("Invalid access token");
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Unauthorized("GitHub token expired or invalid");
            }
            return StatusCode(500, "Error fetching GitHub repositories");
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("resume")]
    [Authorize]
    public async Task<IActionResult> SaveResumeData([FromBody] ResumeDataRequest request)
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

            // Find or create user in our database
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.GitHubLogin == githubUser.Login);
            
            if (existingUser == null)
            {
                existingUser = new PortfolioGenerator.Models.User
                {
                    GitHubLogin = githubUser.Login,
                    Name = githubUser.Name ?? githubUser.Login
                };
                _context.Users.Add(existingUser);
            }

            // Update user information
            existingUser.Email = request.Email;
            existingUser.LinkedIn = request.LinkedIn;
            existingUser.Summary = request.ProfessionalSummary;
            existingUser.ContactInfo = request.ContactInfo;

            // Remove existing selected repositories
            var existingSelectedRepos = _context.SelectedRepositories.Where(sr => sr.UserId == existingUser.Id).ToList();
            _context.SelectedRepositories.RemoveRange(existingSelectedRepos);

            // Add new selected repositories
            foreach (var repo in request.SelectedRepositories)
            {
                var selectedRepo = new SelectedRepository
                {
                    UserId = existingUser.Id,
                    RepoId = repo.RepoId,
                    Name = repo.Name,
                    Description = repo.Description,
                    CustomDescription = repo.CustomDescription,
                    Language = repo.Language,
                    StarCount = repo.StarCount,
                    Url = repo.Url,
                    CustomTitle = repo.CustomTitle,
                    CustomBulletPoints = repo.CustomBulletPoints ?? new List<string>(),
                    AddedAt = DateTime.UtcNow
                };
                _context.SelectedRepositories.Add(selectedRepo);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Resume data saved successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("resume")]
    [Authorize]
    public async Task<IActionResult> GetResumeData()
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

            var user = await _context.Users
                .Include(u => u.SelectedRepositories)
                .FirstOrDefaultAsync(u => u.GitHubLogin == githubUser.Login);

            if (user == null)
            {
                return NotFound("User not found in database");
            }

            var resumeData = new ResumeDataResponse
            {
                User = new UserInfo
                {
                    GitHubLogin = user.GitHubLogin,
                    Name = user.Name,
                    Email = user.Email,
                    LinkedIn = user.LinkedIn,
                    ProfessionalSummary = user.Summary,
                    ContactInfo = user.ContactInfo
                },
                SelectedRepositories = user.SelectedRepositories.Select(sr => new SelectedRepoInfo
                {
                    RepoId = sr.RepoId,
                    Name = sr.Name,
                    Description = sr.Description,
                    CustomDescription = sr.CustomDescription,
                    Language = sr.Language,
                    StarCount = sr.StarCount,
                    Url = sr.Url,
                    CustomTitle = sr.CustomTitle,
                    CustomBulletPoints = sr.CustomBulletPoints
                }).ToList()
            };

            return Ok(resumeData);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
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
    public List<string> CustomBulletPoints { get; set; } = new();
}