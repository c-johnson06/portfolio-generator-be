using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Microsoft.EntityFrameworkCore;
using PortfolioGenerator.Data;
using PortfolioGenerator.Models.DTOs;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class PdfController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PdfController> _logger;

    public PdfController(ApplicationDbContext context, ILogger<PdfController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("resume/{username}/pdf")]
    [AllowAnonymous]
    public async Task<IActionResult> GeneratePdf(string username)
    {
        try
        {
            _logger.LogInformation("Generating PDF for user: {Username}", username);

            var user = await _context.Users
                .Include(u => u.SelectedRepositories)
                .FirstOrDefaultAsync(u => u.GitHubLogin.ToLower() == username.ToLower());

            if (user == null)
            {
                _logger.LogWarning("User not found for PDF generation: {Username}", username);
                return NotFound("User not found");
            }

            // Deserialize skills from JSON
            List<string> skills = new List<string>();
            if (!string.IsNullOrEmpty(user.SkillsJson))
            {
                try
                {
                    skills = JsonSerializer.Deserialize<List<string>>(user.SkillsJson) ?? new List<string>();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing skills for user: {Username}", username);
                    skills = new List<string>();
                }
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
                Skills = skills,
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

            var htmlContent = GenerateHtmlTemplate(resumeData);

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
            };

            var browserFetcher = new BrowserFetcher();
            var revisionInfo = await browserFetcher.DownloadAsync();
            launchOptions.ExecutablePath = revisionInfo.GetExecutablePath();

            using var browser = await Puppeteer.LaunchAsync(launchOptions);
            using var page = await browser.NewPageAsync();

            await page.SetContentAsync(htmlContent);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "20px",
                    Bottom = "20px",
                    Left = "30px",
                    Right = "30px"
                },
                DisplayHeaderFooter = false,
                PreferCSSPageSize = true
            });

            _logger.LogInformation("PDF generated successfully for user: {Username}", username);

            return File(pdfBytes, "application/pdf", $"{username}_resume.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF for user: {Username}", username);
            return StatusCode(500, "Error generating PDF");
        }
    }

    private string GenerateHtmlTemplate(ResumeDataResponse resumeData)
    {
        var html = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset=""utf-8"">
        <title>{resumeData.User.Name} - Resume</title>
        <style>
            @import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap');
            
            * {{
                margin: 0;
                padding: 0;
                box-sizing: border-box;
            }}
            
            body {{
                font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                font-size: 11pt;
                line-height: 1.6;
                color: #374151;
                background: white;
            }}
            
            .container {{
                width: 100%;
                max-width: 100%;
                margin: 0 auto;
                padding: 0.5in;
            }}
            
            .header {{
                margin-bottom: 0.3in;
                border-bottom: 1pt solid #d1d5db;
                padding-bottom: 0.2in;
            }}
            
            .name {{
                font-size: 16pt;
                font-weight: 700;
                color: #111827;
                margin-bottom: 0.1in;
            }}
            
            .contact-info {{
                display: flex;
                flex-wrap: wrap;
                gap: 0.25in;
                font-size: 10pt;
                color: #4b5563;
            }}
            
            .contact-item {{
                display: flex;
                align-items: center;
                gap: 0.1in;
            }}
            
            .contact-icon {{
                width: 0.4in;
                height: 0.4in;
            }}
            
            .summary {{
                margin-top: 0.2in;
                font-size: 10pt;
                color: #374151;
            }}

            .section {{
                margin-bottom: 0.25in;
            }}
            
            .section-title {{
                font-size: 13pt;
                font-weight: 700;
                color: #111827;
                margin-bottom: 0.15in;
                padding-bottom: 0.05in;
                border-bottom: 1pt solid #d1d5db;
            }}
            
            .skills-container {{
                display: flex;
                flex-wrap: wrap;
                gap: 0.1in;
            }}
            
            .skill-tag {{
                background-color: #f3f4f6;
                border: 1pt solid #d1d5db;
                border-radius: 0.1in;
                padding: 0.05in 0.15in;
                font-size: 9pt;
                font-weight: 500;
                color: #1f2937;
            }}
            
            .projects-container {{
                display: flex;
                flex-direction: column;
                gap: 0.2in;
            }}
            
            .project {{
                break-inside: avoid;
                margin-bottom: 0.15in;
            }}
            
            .project-header {{
                display: flex;
                justify-content: space-between;
                align-items: baseline;
                margin-bottom: 0.05in;
            }}
            
            .project-title {{
                font-size: 12pt;
                font-weight: 600;
                color: #111827;
            }}
            
            .project-link {{
                font-size: 9pt;
                color: #2563eb;
                text-decoration: none;
            }}
            
            .project-link:hover {{
                text-decoration: underline;
            }}
            
            .project-meta {{
                display: flex;
                flex-wrap: wrap;
                gap: 0.15in;
                font-size: 9pt;
                color: #6b7280;
                margin-bottom: 0.1in;
            }}
            
            .project-description {{
                font-size: 10pt;
                margin-bottom: 0.1in;
                color: #374151;
            }}
            
            .bullet-list {{
                padding-left: 0.2in;
                margin-top: 0;
                margin-bottom: 0;
            }}
            
            .bullet-list li {{
                margin-bottom: 0.03in;
                font-size: 10pt;
                color: #374151;
            }}
            
            .bullet-list li:last-child {{
                margin-bottom: 0;
            }}
            
            @media print {{
                body {{
                    -webkit-print-color-adjust: exact;
                    print-color-adjust: exact;
                }}
                .container {{
                    padding: 0;
                }}
            }}
        </style>
    </head>
    <body>
        <div class=""container"">
            <!-- Header Section -->
            <header class=""header"">
                <h1 class=""name"">{resumeData.User.Name}</h1>
                <div class=""contact-info"">
                    {(string.IsNullOrEmpty(resumeData.User.Email) ? "" : $@"
                    <div class=""contact-item"">
                        <svg class=""contact-icon"" fill=""none"" stroke=""currentColor"" viewBox=""0 0 24 24"">
                            <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""1.5"" d=""M3 8l7.89 4.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z""></path>
                        </svg>
                        <span>{resumeData.User.Email}</span>
                    </div>
                    ")}
                    {(string.IsNullOrEmpty(resumeData.User.LinkedIn) ? "" : $@"
                    <div class=""contact-item"">
                        <svg class=""contact-icon"" fill=""currentColor"" viewBox=""0 0 24 24"">
                            <path d=""M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433c-1.144 0-2.063-.926-2.063-2.065 0-1.138.92-2.063 2.063-2.063 1.14 0 2.064.925 2.064 2.063 0 1.139-.925 2.065-2.064 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z""></path>
                        </svg>
                        <span>{resumeData.User.LinkedIn}</span>
                    </div>
                    ")}
                    <div class=""contact-item"">
                        <svg class=""contact-icon"" fill=""currentColor"" viewBox=""0 0 24 24"">
                            <path d=""M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z""></path>
                        </svg>
                        <span>github.com/{resumeData.User.GitHubLogin}</span>
                    </div>
                </div>
                {(string.IsNullOrEmpty(resumeData.User.ProfessionalSummary) ? "" : $@"
                <p class=""summary"">{resumeData.User.ProfessionalSummary}</p>
                ")}
            </header>

            <!-- Skills Section -->
            {(resumeData.Skills.Any() ? $@"
            <section class=""section"">
                <h2 class=""section-title"">Skills</h2>
                <div class=""skills-container"">
                    {string.Join("", resumeData.Skills.Select(skill => $@"
                    <span class=""skill-tag"">{skill}</span>
                    "))}
                </div>
            </section>
            " : "")}

            <!-- Projects Section -->
            <section class=""section"">
                <h2 class=""section-title"">Projects</h2>
                <div class=""projects-container"">
                    {string.Join("", resumeData.SelectedRepositories.Select(repo => $@"
                    <div class=""project"">
                        <div class=""project-header"">
                            <h3 class=""project-title"">{(string.IsNullOrEmpty(repo.CustomTitle) ? repo.Name : repo.CustomTitle)}</h3>
                            <a href=""{repo.Url}"" target=""_blank"" rel=""noopener noreferrer"" class=""project-link"">View Project</a>
                        </div>
                        <div class=""project-meta"">
                            {(string.IsNullOrEmpty(repo.Language) ? "" : $@"
                            <span class=""language"">{repo.Language}</span>
                            ")}
                            <span class=""date"">{DateTime.Parse(repo.AddedAt):MMM yyyy}</span>
                        </div>
                        <div class=""project-description"">
                            {(repo.CustomBulletPoints.Any() ? $@"
                            <ul class=""bullet-list"">
                                {string.Join("", repo.CustomBulletPoints.Select(point => $@"<li>{point}</li>"))}
                            </ul>
                            " : "")}
                        </div>
                    </div>
                    "))}
                </div>
            </section>
        </div>
    </body>
    </html>";

        return html;
    }
}