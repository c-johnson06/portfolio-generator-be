using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Microsoft.EntityFrameworkCore;
using PortfolioGenerator.Data;
using PortfolioGenerator.Models.DTOs;

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
                    AddedAt = sr.AddedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") // ISO 8601 format
                }).ToList()
            };

            var htmlContent = GenerateHtmlTemplate(resumeData);

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
            };

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
                    font-size: 12px;
                    line-height: 1.5;
                    color: #1f2937;
                    background: white;
                }}
                
                .max-w-4xl {{
                    max-width: 896px;
                    margin: 0 auto;
                }}
                
                .mx-auto {{
                    margin-left: auto;
                    margin-right: auto;
                }}
                
                .p-8 {{
                    padding: 2rem;
                }}
                
                .bg-white {{
                    background-color: white;
                }}
                
                .text-gray-900 {{
                    color: #111827;
                }}
                
                .font-sans {{
                    font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                }}
                
                .mb-8 {{
                    margin-bottom: 2rem;
                }}
                
                .pb-6 {{
                    padding-bottom: 1.5rem;
                }}
                
                .border-b {{
                    border-bottom-width: 1px;
                }}
                
                .border-gray-200 {{
                    border-color: #e5e7eb;
                }}
                
                .text-3xl {{
                    font-size: 1.875rem;
                    line-height: 2.25rem;
                }}
                
                .font-bold {{
                    font-weight: 700;
                }}
                
                .text-gray-700 {{
                    color: #374151;
                }}
                
                .leading-relaxed {{
                    line-height: 1.625;
                }}
                
                .flex {{
                    display: flex;
                }}
                
                .flex-wrap {{
                    flex-wrap: wrap;
                }}
                
                .gap-4 {{
                    gap: 1rem;
                }}
                
                .items-center {{
                    align-items: center;
                }}
                
                .text-sm {{
                    font-size: 0.875rem;
                    line-height: 1.25rem;
                }}
                
                .text-gray-600 {{
                    color: #4b5563;
                }}
                
                .mt-4 {{
                    margin-top: 1rem;
                }}
                
                .h-4 {{
                    height: 1rem;
                }}
                
                .w-4 {{
                    width: 1rem;
                }}
                
                .mb-3 {{
                    margin-bottom: 0.75rem;
                }}
                
                .text-xl {{
                    font-size: 1.25rem;
                    line-height: 1.75rem;
                }}
                
                .pb-2 {{
                    padding-bottom: 0.5rem;
                }}
                
                .space-y-6 > * + * {{
                    margin-top: 1.5rem;
                }}
                
                .break-inside-avoid {{
                    page-break-inside: avoid;
                }}
                
                .border {{
                    border-width: 1px;
                }}
                
                .border-gray-200 {{
                    border-color: #e5e7eb;
                }}
                
                .shadow-none {{
                    box-shadow: none;
                }}
                
                .pb-3 {{
                    padding-bottom: 0.75rem;
                }}
                
                .text-lg {{
                    font-size: 1.125rem;
                    line-height: 1.75rem;
                }}
                
                .font-semibold {{
                    font-weight: 600;
                }}
                
                .justify-between {{
                    justify-content: space-between;
                }}
                
                .items-start {{
                    align-items: flex-start;
                }}
                
                .mt-1 {{
                    margin-top: 0.25rem;
                }}
                
                .flex-wrap {{
                    flex-wrap: wrap;
                }}
                
                .gap-3 {{
                    gap: 0.75rem;
                }}
                
                .text-sm {{
                    font-size: 0.875rem;
                }}
                
                .space-y-1 > * + * {{
                    margin-top: 0.25rem;
                }}
                
                .flex.items-start {{
                    display: flex;
                    align-items: flex-start;
                }}
                
                .mr-2 {{
                    margin-right: 0.5rem;
                }}
                
                .text-blue-600 {{
                    color: #2563eb;
                }}
                
                .pt-8 {{
                    padding-top: 2rem;
                }}
                
                .text-xs {{
                    font-size: 0.75rem;
                    line-height: 1rem;
                }}
                
                .text-center {{
                    text-align: center;
                }}
                
                .border-t {{
                    border-top-width: 1px;
                }}
                
                .border-gray-200 {{
                    border-color: #e5e7eb;
                }}
                
                .gap-1 {{
                    gap: 0.25rem;
                }}
                
                .h-3 {{
                    height: 0.75rem;
                }}
                
                .w-3 {{
                    width: 0.75rem;
                }}
                
                .hover\:text-blue-800:hover {{
                    color: #1d4ed8;
                }}
                
                .flex-wrap {{
                    flex-wrap: wrap;
                }}
                
                .gap-2 {{
                    gap: 0.5rem;
                }}
                
                .py-1 {{
                    padding-top: 0.25rem;
                    padding-bottom: 0.25rem;
                }}
                
                .px-3 {{
                    padding-left: 0.75rem;
                    padding-right: 0.75rem;
                }}
                
                .text-sm {{
                    font-size: 0.875rem;
                }}
                
                .border {{
                    border-width: 1px;
                }}
                
                .border-gray-300 {{
                    border-color: #d1d5db;
                }}
                
                .rounded {{
                    border-radius: 0.25rem;
                }}
                
                .bg-gray-100 {{
                    background-color: #f3f4f6;
                }}
                
                .text-gray-800 {{
                    color: #1f2937;
                }}
                
                .font-medium {{
                    font-weight: 500;
                }}
                
                @media print {{
                    body {{
                        -webkit-print-color-adjust: exact;
                        print-color-adjust: exact;
                    }}
                }}
            </style>
        </head>
        <body>
            <div class=""max-w-4xl mx-auto p-8 bg-white text-gray-900 font-sans"">
                <!-- Header Section -->
                <header class=""mb-8 pb-6 border-b border-gray-200"">
                    <h1 class=""text-3xl font-bold text-gray-900 mb-2"">{resumeData.User.Name}</h1>
                    <div class=""flex flex-wrap gap-4 text-sm text-gray-600"">
                        <div class=""flex items-center gap-1"">
                            <svg class=""h-4 w-4"" fill=""none"" stroke=""currentColor"" viewBox=""0 0 24 24"">
                                <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M3 8l7.89 4.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z""></path>
                            </svg>
                            <span>{resumeData.User.Email}</span>
                        </div>
                        <div class=""flex items-center gap-1"">
                            <svg class=""h-4 w-4"" fill=""currentColor"" viewBox=""0 0 24 24"">
                                <path d=""M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433c-1.144 0-2.063-.926-2.063-2.065 0-1.138.92-2.063 2.063-2.063 1.14 0 2.064.925 2.064 2.063 0 1.139-.925 2.065-2.064 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z""></path>
                            </svg>
                            <span>{resumeData.User.LinkedIn}</span>
                        </div>
                        <div class=""flex items-center gap-1"">
                            <svg class=""h-4 w-4"" fill=""currentColor"" viewBox=""0 0 24 24"">
                                <path d=""M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z""></path>
                            </svg>
                            <span>github.com/{resumeData.User.GitHubLogin}</span>
                        </div>
                    </div>
                    {(string.IsNullOrEmpty(resumeData.User.ProfessionalSummary) ? "" : $@"
                    <p class=""mt-4 text-gray-700 leading-relaxed"">
                        {resumeData.User.ProfessionalSummary}
                    </p>
                    ")}
                </header>

                <!-- Skills Section -->
                {(resumeData.SelectedRepositories.Count == 0 ? "" : $@"
                <section class=""mb-8"">
                    <h2 class=""text-xl font-bold text-gray-900 mb-3 pb-2 border-b border-gray-300"">
                        Skills & Technologies
                    </h2>
                    <div class=""flex flex-wrap gap-2"">
                        {(string.Join("", resumeData.SelectedRepositories
                            .Where(r => !string.IsNullOrEmpty(r.Language))
                            .Select(r => r.Language)
                            .Distinct()
                            .Select(lang => $@"
                        <span class=""py-1 px-3 text-sm font-medium bg-gray-100 text-gray-800 rounded border border-gray-300"">
                            {lang}
                        </span>
                        ")))}
                    </div>
                </section>
                ")}

                <!-- Projects Section -->
                <section class=""mb-8"">
                    <h2 class=""text-xl font-bold text-gray-900 mb-4 pb-2 border-b border-gray-300"">
                        Projects
                    </h2>
                    <div class=""space-y-6"">
                        {string.Join("", resumeData.SelectedRepositories.Select(repo => $@"
                        <div class=""break-inside-avoid"">
                            <div class=""border border-gray-200 shadow-none"">
                                <div class=""pb-3"">
                                    <div class=""flex justify-between items-start"">
                                        <div>
                                            <h3 class=""text-lg font-semibold text-gray-900"">
                                                {(string.IsNullOrEmpty(repo.CustomTitle) ? repo.Name : repo.CustomTitle)}
                                            </h3>
                                            <div class=""flex flex-wrap items-center gap-3 mt-1 text-sm"">
                                                <div class=""flex items-center gap-1 text-gray-600"">
                                                    <svg class=""h-3 w-3"" fill=""none"" stroke=""currentColor"" viewBox=""0 0 24 24"">
                                                        <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4""></path>
                                                    </svg>
                                                    <span>{(string.IsNullOrEmpty(repo.Language) ? "N/A" : repo.Language)}</span>
                                                </div>
                                                <div class=""flex items-center gap-1 text-gray-600"">
                                                    <svg class=""h-3 w-3"" fill=""currentColor"" viewBox=""0 0 20 20"">
                                                        <path d=""M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z"" />
                                                    </svg>
                                                    <span>{repo.StarCount}</span>
                                                </div>
                                                <div class=""flex items-center gap-1 text-gray-600"">
                                                    <svg class=""h-3 w-3"" fill=""none"" stroke=""currentColor"" viewBox=""0 0 24 24"">
                                                        <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"" />
                                                    </svg>
                                                    <span>{DateTime.Parse(repo.AddedAt).ToString("MMM yyyy")}</span>
                                                </div>
                                            </div>
                                        </div>
                                        <a href=""{repo.Url}"" target=""_blank"" rel=""noopener noreferrer"" class=""text-blue-600 hover:text-blue-800"">
                                            <svg class=""h-4 w-4"" fill=""none"" stroke=""currentColor"" viewBox=""0 0 24 24"">
                                                <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"" />
                                            </svg>
                                        </a>
                                    </div>
                                </div>
                                <div class=""pb-3"">
                                    <p class=""text-gray-700 mb-3"">
                                        {(string.IsNullOrEmpty(repo.CustomDescription) ? repo.Description : repo.CustomDescription)}
                                    </p>
                                    {(repo.CustomBulletPoints.Count == 0 ? "" : $@"
                                    <ul class=""space-y-1"">
                                        {string.Join("", repo.CustomBulletPoints.Select(point => $@"
                                        <li class=""flex items-start"">
                                            <span class=""mr-2 text-blue-600"">•</span>
                                            <span class=""text-gray-700"">{point}</span>
                                        </li>
                                        "))}
                                    </ul>
                                    ")}
                                </div>
                            </div>
                        </div>
                        "))}
                    </div>
                </section>

                <!-- Footer -->
                <footer class=""text-center text-xs text-gray-500 pt-8 border-t border-gray-200"">
                    <p>Generated from PortfolioGen • {DateTime.Now.Year}</p>
                </footer>
            </div>
        </body>
        </html>";

        return html;
    }
}