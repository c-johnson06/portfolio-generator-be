using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Octokit;
using PortfolioGenerator.Data;
using PortfolioGenerator.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using PortfolioGenerator.Models;
using System.Security.Claims;

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

    [HttpPost("resume")]
    [Authorize]
    public async Task<IActionResult> SaveResumeData([FromBody] ResumeDataRequest request)
    {
        try
        {
            var githubLogin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(githubLogin))
            {
                return Unauthorized("Could not determine GitHub login from token.");
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.GitHubLogin == githubLogin);

            if (existingUser == null)
            {
                return NotFound("User not found in the database.");
            }

            // Update user information
            existingUser.Email = request.Email;
            existingUser.LinkedIn = request.LinkedIn;
            existingUser.Summary = request.ProfessionalSummary;

            // Remove existing selected repositories to replace them
            var existingSelectedRepos = _context.SelectedRepositories.Where(sr => sr.UserId == existingUser.Id);
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
            _logger.LogError(ex, "Error saving resume data");
            return StatusCode(500, "An internal server error occurred.");
        }
    }

    [HttpGet("resume")]
    [Authorize]
    public async Task<IActionResult> GetResumeData()
    {
        try
        {
            var githubLogin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(githubLogin))
            {
                return Unauthorized("Could not determine GitHub login from token.");
            }

            var user = await _context.Users
                .Include(u => u.SelectedRepositories)
                .FirstOrDefaultAsync(u => u.GitHubLogin == githubLogin);

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
                    ProfessionalSummary = user.Summary
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
                    CustomBulletPoints = sr.CustomBulletPoints,
                    AddedAt = sr.AddedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }).ToList()
            };

            return Ok(resumeData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resume data");
            return StatusCode(500, "An internal server error occurred.");
        }
    }
}