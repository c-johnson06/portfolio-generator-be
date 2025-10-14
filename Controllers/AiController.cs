using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Octokit;
using Groq; 
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Claims;
using PortfolioGenerator.Data;
using Microsoft.EntityFrameworkCore;
using System.Text; 

[ApiController]
[Route("api/[controller]")]
public class AiController : ControllerBase
{
    private readonly ILogger<AiController> _logger;
    private readonly GroqClient _groqClient;
    private readonly ApplicationDbContext _context;

    public AiController(IConfiguration configuration, ILogger<AiController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        var groqApiKey = configuration["Groq:ApiKey"];
        if (string.IsNullOrEmpty(groqApiKey))
        {
            throw new ArgumentException("Groq API key is missing from configuration.", nameof(groqApiKey));
        }
        _groqClient = new GroqClient(groqApiKey);
        _context = context;
    }

[HttpPost("generate-bullets")]
[Authorize]
public async Task<IActionResult> GenerateRepoBulletPoints([FromBody] GenerateRequest request)
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

        string readmeContent;
        try
        {
            var readme = await github.Repository.Content.GetReadme(request.Owner, request.RepoName);
            readmeContent = readme.Content;
        }
        catch (NotFoundException)
        {
            return NotFound(new { message = "Could not find a README file for this repository." });
        }

        IList<ChatCompletionRequestMessage> messages = [
            new ChatCompletionRequestSystemMessage{
                Role = ChatCompletionRequestSystemMessageRole.System,
                Content = "You are an expert resume writer. Your task is to generate 4-5 concise, impactful bullet points for a software developer's resume based on a project's README file. Each bullet point should start with a unique action verb and highlight a technical achievement, a key feature, or the problem the project solves. Focus on quantifiable results if possible. **CRITICAL INSTRUCTION: Respond ONLY with a valid JSON object. The JSON must contain a single key 'bulletPoints' which is an array of strings. Do not include any other text, explanations, or markdown code blocks like ```json.** Example: {\"bulletPoints\":[\"Developed a feature using C# and ASP.NET Core.\", \"Increased performance by 20%.\", \"Implemented user authentication.\"]}",
            },
            new ChatCompletionRequestUserMessage {
                Role = ChatCompletionRequestUserMessageRole.User,
                Content = $"Here is the README content for the project '{request.RepoName}':\n\n---\n{readmeContent}\n---"
            }
        ];

        CreateChatCompletionRequest prompt = new()
        {
            Messages = messages,
            Model = "llama-3.1-8b-instant" 
        };

        var response = await _groqClient.Chat.CreateChatCompletionAsync(prompt);

        var rawResponse = response.Choices[0].Message.Content;

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            _logger.LogWarning("AI returned an empty response for {Owner}/{RepoName}", request.Owner, request.RepoName);
            return BadRequest(new { message = "AI service returned an empty response." });
        }

        // --- ROBUST PARSING LOGIC ---
        _logger.LogDebug("Raw AI response for {Owner}/{RepoName}: {RawResponse}", request.Owner, request.RepoName, rawResponse);

        // 1. Trim whitespace
        var jsonResponse = rawResponse.Trim();

        // 2. Attempt to find JSON object within potential wrapper text/markdown
        // Look for the first '{' and last '}'
        int startIndex = jsonResponse.IndexOf('{');
        int endIndex = jsonResponse.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            jsonResponse = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
            _logger.LogDebug("Extracted potential JSON: {JsonResponse}", jsonResponse);
        }
        else
        {
             _logger.LogWarning("Could not locate JSON object boundaries in AI response for {Owner}/{RepoName}", request.Owner, request.RepoName);
             // Fall back to trying to parse the trimmed raw response directly
             // This might still fail, which is caught below.
        }

        // 3. Try to parse the extracted or raw JSON string
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("bulletPoints", out var bulletPointsElement) && bulletPointsElement.ValueKind == JsonValueKind.Array)
            {
                var bulletPoints = bulletPointsElement.EnumerateArray()
                    .Select(element => element.GetString() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();

                if (bulletPoints.Length == 0)
                {
                    _logger.LogWarning("Parsed JSON for {Owner}/{RepoName} contained an empty 'bulletPoints' array.", request.Owner, request.RepoName);
                    return Ok(new { bulletPoints = new string[] { "Generated bullet point placeholder." } }); // Or handle empty array as needed
                }

                return Ok(new { bulletPoints });
            }
            else
            {
                _logger.LogWarning("Parsed JSON for {Owner}/{RepoName} did not contain a 'bulletPoints' array property.", request.Owner, request.RepoName);
                // Consider returning a default or error state
                 return BadRequest(new { message = "AI response JSON was malformed (missing 'bulletPoints' array)." });
            }
        }
        catch (JsonException parseEx)
        {
            _logger.LogError(parseEx, "Failed to parse extracted JSON '{JsonString}' for {Owner}/{RepoName}. Raw response was: {RawResponse}", jsonResponse, request.Owner, request.RepoName, rawResponse);
            // Return a user-friendly error
            return StatusCode(500, new { message = "Failed to interpret AI response. The format was not as expected." });
        }

    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error generating AI bullet points for {Owner}/{RepoName}", request.Owner, request.RepoName);
        return StatusCode(500, new { message = "An internal server error occurred while generating bullet points." });
    }
}

    [HttpPost("generate-cover-letter")]
    [Authorize]
    public async Task<IActionResult> GenerateCoverLetter([FromBody] CoverLetterRequest request)
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

            var reposToProcess = request.RepoNames.Take(4).ToList();
            var readmeContents = new List<string>();

            foreach (var repoName in reposToProcess)
            {
                try
                {
                    var readme = await github.Repository.Content.GetReadme(request.Owner, repoName);
                    readmeContents.Add($"--- Repository: {repoName} ---\n{readme.Content}\n--- End of {repoName} ---");
                }
                catch (NotFoundException)
                {
                    _logger.LogWarning("README not found for repository: {Owner}/{RepoName}", request.Owner, repoName);
                    try
                    {
                        var repoDetails = await github.Repository.Get(request.Owner, repoName);
                        var detailsText = $"--- Repository: {repoName} (No README) ---\n";
                        detailsText += $"Description: {repoDetails.Description ?? "No description provided"}\n";
                        detailsText += $"Language: {repoDetails.Language ?? "Not specified"}\n";
                        detailsText += $"Stars: {repoDetails.StargazersCount}\n";
                        detailsText += $"Forks: {repoDetails.ForksCount}\n";
                        detailsText += $"URL: {repoDetails.HtmlUrl}\n";
                        readmeContents.Add(detailsText);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error fetching repository details for {Owner}/{RepoName}", request.Owner, repoName);
                        continue;
                    }
                }
            }

            if (!readmeContents.Any())
            {
                 return BadRequest(new { message = "No README files found for the selected repositories." });
            }

            var allReadmeContent = string.Join("\n\n", readmeContents);

            var promptContent = $@"
Job Description:
{request.PositionRequirements}

Project Details (from READMEs):
{allReadmeContent}

Instructions:
Using the project details provided, write a professional and compelling cover letter tailored specifically to the job description above. 
- Highlight relevant skills, technologies, and experiences demonstrated in the projects.
-A 3-paragraph cover letter begins with an introduction that states your purpose for writing and how you learned of the position, then moves to the body paragraphs to detail your skills, experience, and qualifications, linking them directly to the job requirements and demonstrating your research of the company. The final paragraph concludes the letter by expressing your gratitude for the opportunity to be considered, reiterating your interest, and suggesting a call to action, such as a follow-up or interview. 
- Connect the projects' features, problems solved, and outcomes to the requirements and responsibilities mentioned in the job description.
- Maintain a professional tone and structure.
- The cover letter should be addressed to the hiring manager and signed off by the applicant (use the GitHub username: {request.Owner}).

Return the complete cover letter as a single string.";

            IList<ChatCompletionRequestMessage> messages = [
                new ChatCompletionRequestSystemMessage{
                    Role = ChatCompletionRequestSystemMessageRole.System,
                    Content = "You are an expert cover letter writer for software developers. You create personalized, professional, and compelling cover letters based on a user's projects (from READMEs) and a target job description."
                },
                new ChatCompletionRequestUserMessage {
                    Role = ChatCompletionRequestUserMessageRole.User,
                    Content = promptContent
                }
            ];

            CreateChatCompletionRequest prompt = new()
            {
                Messages = messages,
                Model = "llama-3.1-8b-instant" 
            };

            var response = await _groqClient.Chat.CreateChatCompletionAsync(prompt);
            var coverLetter = response.Choices[0].Message.Content;

            if (string.IsNullOrEmpty(coverLetter))
            {
                throw new ArgumentException("AI response for cover letter was empty or null");
            }

            var cleanedCoverLetter = coverLetter.Trim();

            return Ok(new { coverLetter = cleanedCoverLetter });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating cover letter for {Owner}", request.Owner);
            return StatusCode(500, new { message = "An internal server error occurred while generating the cover letter." });
        }
    }

    // --- MODIFIED COMPARE PORTFOLIO ENDPOINT USING GROQ ---
    [HttpPost("compare-portfolio")]
    [Authorize]
    public async Task<IActionResult> ComparePortfolio([FromBody] ComparePortfolioRequest request)
    {
        try
        {
            // 1. Authenticate the user and get their GitHub access token
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized("Could not read access token");
            }

            var github = new GitHubClient(new ProductHeaderValue("Portfolio-Generator"))
            {
                Credentials = new Credentials(accessToken)
            };

            // 2. Get the authenticated GitHub user's login
            var githubUser = await github.User.Current();
            var githubLogin = githubUser.Login;

            // 3. Fetch the corresponding user from your database
            var user = await _context.Users.FirstOrDefaultAsync(u => u.GitHubLogin == githubLogin);
            if (user == null)
            {
                return NotFound("User not found in the database");
            }

            // 4. Fetch the user's *previously saved* selected repositories from the database
            var selectedRepos = await _context.SelectedRepositories
                .Where(sr => sr.UserId == user.Id)
                .ToListAsync();

            if (!selectedRepos.Any())
            {
                return BadRequest("No selected repositories found for analysis. Please save a portfolio with selected repositories first.");
            }

            // 5. Fetch details for each selected repository from GitHub to get languages, topics, READMEs
            var repoDetails = new List<RepoAnalysisDetails>();
            foreach (var repo in selectedRepos)
            {
                try
                {
                    // Get core repository details (includes topics)
                    var githubRepo = await github.Repository.Get(githubLogin, repo.Name);
                    
                    // Get languages used in the repository
                    var languagesResponse = await github.Repository.GetAllLanguages(githubLogin, repo.Name);
                    var languages = languagesResponse.Select(l => l.Name).ToList();
                    
                    // Get README content if it exists
                    string readmeContent = "";
                    try
                    {
                        var readme = await github.Repository.Content.GetReadme(githubLogin, repo.Name);
                        readmeContent = readme.Content;
                    }
                    catch (NotFoundException)
                    {
                        // README not found, continue with other data
                        _logger.LogDebug("README not found for {Owner}/{RepoName}", githubLogin, repo.Name);
                    }

                    // Compile the details for analysis
                    repoDetails.Add(new RepoAnalysisDetails
                    {
                        Name = repo.Name,
                        Languages = languages,
                        Topics = githubRepo.Topics?.ToList() ?? new List<string>(),
                        CustomTitle = repo.CustomTitle ?? repo.Name,
                        CustomDescription = repo.CustomDescription ?? "",
                        CustomBulletPoints = repo.CustomBulletPoints ?? new List<string>(),
                        ReadmeContent = readmeContent
                    });
                }
                catch (Exception ex) // Catch general exceptions for individual repos
                {
                    _logger.LogWarning(ex, "Error fetching details for repository {Owner}/{RepoName}", githubLogin, repo.Name);
                    // Depending on requirements, you might choose to:
                    // 1. Skip the repo and continue (current behavior)
                    // 2. Return an error if any repo fails
                    // 3. Include the repo with partial data from the DB
                    // For now, we'll skip and log the error.
                    continue; 
                }
            }

            if (!repoDetails.Any())
            {
                 return BadRequest(new { message = "Could not fetch details for any of the selected repositories." });
            }

            // 6. --- CORE CHANGE: USE GROQ FOR SEMANTIC ANALYSIS ---
            // Construct a prompt for the LLM to perform the comparative analysis
            
            // a. Build the project details string for the prompt
            var projectDetailsBuilder = new StringBuilder();
            foreach (var repo in repoDetails)
            {
                projectDetailsBuilder.AppendLine($"--- Project: {repo.Name} ---");
                projectDetailsBuilder.AppendLine($"Title: {repo.CustomTitle}");
                if (!string.IsNullOrEmpty(repo.CustomDescription))
                {
                    projectDetailsBuilder.AppendLine($"Description: {repo.CustomDescription}");
                }
                if (repo.Languages.Any())
                {
                    projectDetailsBuilder.AppendLine($"Languages: {string.Join(", ", repo.Languages)}");
                }
                if (repo.Topics.Any())
                {
                    projectDetailsBuilder.AppendLine($"Topics/Technologies: {string.Join(", ", repo.Topics)}");
                }
                if (repo.CustomBulletPoints.Any())
                {
                    projectDetailsBuilder.AppendLine("Key Contributions:");
                    foreach(var bullet in repo.CustomBulletPoints)
                    {
                        projectDetailsBuilder.AppendLine($"  - {bullet}");
                    }
                }
                if (!string.IsNullOrEmpty(repo.ReadmeContent))
                {
                     // Truncate README content to prevent excessively long prompts
                     // You might want a smarter truncation that preserves key parts
                     var truncatedReadme = repo.ReadmeContent.Length > 2000 ? repo.ReadmeContent.Substring(0, 2000) + "..." : repo.ReadmeContent;
                     projectDetailsBuilder.AppendLine($"README Snippet (first 2000 chars): {truncatedReadme}");
                }
                projectDetailsBuilder.AppendLine("--- End of Project ---\n");
            }
            var projectDetailsString = projectDetailsBuilder.ToString();

            // b. Construct the full prompt
            var llmPrompt = $@"
Job Description:
{request.JobDescription}

Candidate's Projects:
{projectDetailsString}

Instructions:
Act as an expert career advisor and technical recruiter. Your task is to perform a deep, semantic analysis comparing the candidate's selected projects against the provided job description.

1.  **Skills Identification:**
    *   Based SOLELY on the information within the 'Job Description', identify the core technical skills, programming languages, frameworks, tools, and methodologies explicitly required or strongly desired. Focus on specific, tangible technologies and expertise areas. Provide this list as 'Identified_Job_Skills'.

2.  **Skills Matched:**
    *   By analyzing the 'Candidate's Projects' section, determine which of the 'Identified_Job_Skills' the candidate demonstrably possesses based on their project work (languages used, topics, descriptions, bullet points, README content). List these skills. Provide this list as 'Matched_Skills'.

3.  **Skills Missing/Gaps:**
    *   Identify which of the 'Identified_Job_Skills' are NOT sufficiently demonstrated by the candidate's listed projects. List these skills. Provide this list as 'Missing_Skills'.

4.  **Project Relevance Ranking:**
    *   Rank the candidate's projects from MOST relevant to LEAST relevant to the job description. Justify the ranking briefly based on how well each project's technologies, complexity, and described outcomes align with the job's requirements. Provide this ranked list as 'Ranked_Projects'.

5.  **Overall Summary:**
    *   Provide a concise, maximum 3 sentence summary evaluating how well the candidate's portfolio aligns with the job description. Mention strengths and key areas for improvement or further exploration. Provide this as 'Overall_Summary'.

Format your entire response STRICTLY as a JSON object with the following structure:
{{
  ""identifiedJobSkills"": [""skill1"", ""skill2"", ...],
  ""matchedSkills"": [""matched_skill1"", ""matched_skill2"", ...],
  ""missingSkills"": [""missing_skill1"", ""missing_skill2"", ...],
  ""rankedProjects"": [
    {{""projectName"": ""Project A"", ""relevanceJustification"": ""Brief reason why it's ranked 1st""}},
    {{""projectName"": ""Project B"", ""relevanceJustification"": ""Brief reason why it's ranked 2nd""}},
    ...
  ],
  ""overallSummary"": ""A concise summary of the alignment.""
}}

Ensure the JSON is valid and parseable. Do not include any other text, explanations, or markdown code blocks like ```json.
";

            // c. Prepare messages for Groq
            IList<ChatCompletionRequestMessage> messages = [
                new ChatCompletionRequestSystemMessage{
                    Role = ChatCompletionRequestSystemMessageRole.System,
                    Content = "You are an expert career advisor and technical recruiter. You specialize in analyzing software developer portfolios (projects) against job descriptions to identify skill matches, gaps, and project relevance. You respond with precise, actionable insights in a structured JSON format."
                },
                new ChatCompletionRequestUserMessage {
                    Role = ChatCompletionRequestUserMessageRole.User,
                    Content = llmPrompt
                }
            ];

            // d. Create the Groq completion request
            CreateChatCompletionRequest groqPrompt = new()
            {
                Messages = messages,
                Model = "llama-3.1-8b-instant", // Or your preferred Groq model
                Temperature = 0.1f, // Low temperature for more consistent, factual outputs
                MaxTokens = 2048 // Adjust based on expected output length
            };

            // e. Call Groq API
            var groqResponse = await _groqClient.Chat.CreateChatCompletionAsync(groqPrompt);
            var rawLlmResponse = groqResponse.Choices[0].Message.Content;

            if (string.IsNullOrWhiteSpace(rawLlmResponse))
            {
                 _logger.LogWarning("Groq returned an empty response for comparative analysis.");
                 return StatusCode(500, new { message = "AI service returned an empty response for analysis." });
            }

            // f. --- ROBUSTLY PARSE THE LLM'S JSON RESPONSE ---
            _logger.LogDebug("Raw LLM response for comparative analysis: {RawResponse}", rawLlmResponse);
            
            // Trim whitespace
            var jsonResponse = rawLlmResponse.Trim();
            
            // Attempt to find JSON object within potential wrapper text (extra safety)
            int startIndex = jsonResponse.IndexOf('{');
            int endIndex = jsonResponse.LastIndexOf('}');
            if (startIndex >= 0 && endIndex > startIndex)
            {
                jsonResponse = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                _logger.LogDebug("Extracted potential JSON: {JsonResponse}", jsonResponse);
            }

            try
            {
                // Parse the JSON string into our DTO
                // Using JsonDocument for flexibility in case LLM omits fields slightly
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                var analysisResult = new ComparativeAnalysisResult();

                // Safely extract arrays
                if (root.TryGetProperty("identifiedJobSkills", out var identifiedSkillsElement) && identifiedSkillsElement.ValueKind == JsonValueKind.Array)
                {
                    analysisResult.IdentifiedJobSkills = identifiedSkillsElement.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null)
                        .Where(s => !string.IsNullOrEmpty(s)).Cast<string>().ToList();
                }
                else
                {
                    analysisResult.IdentifiedJobSkills = new List<string>();
                     _logger.LogWarning("LLM response missing or invalid 'identifiedJobSkills' array.");
                }

                if (root.TryGetProperty("matchedSkills", out var matchedSkillsElement) && matchedSkillsElement.ValueKind == JsonValueKind.Array)
                {
                    analysisResult.MatchedSkills = matchedSkillsElement.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null)
                        .Where(s => !string.IsNullOrEmpty(s)).Cast<string>().ToList();
                }
                else
                {
                     analysisResult.MatchedSkills = new List<string>();
                     _logger.LogWarning("LLM response missing or invalid 'matchedSkills' array.");
                }

                if (root.TryGetProperty("missingSkills", out var missingSkillsElement) && missingSkillsElement.ValueKind == JsonValueKind.Array)
                {
                    analysisResult.MissingSkills = missingSkillsElement.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null)
                        .Where(s => !string.IsNullOrEmpty(s)).Cast<string>().ToList();
                }
                else
                {
                     analysisResult.MissingSkills = new List<string>();
                     _logger.LogWarning("LLM response missing or invalid 'missingSkills' array.");
                }

                // Safely extract ranked projects array
                if (root.TryGetProperty("rankedProjects", out var rankedProjectsElement) && rankedProjectsElement.ValueKind == JsonValueKind.Array)
                {
                    analysisResult.RankedProjects = rankedProjectsElement.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.Object) // Ensure it's an object
                        .Select(item =>
                        {
                            var name = item.TryGetProperty("projectName", out var nameElement) && nameElement.ValueKind == JsonValueKind.String ? nameElement.GetString() : "Unknown Project";
                            var justification = item.TryGetProperty("relevanceJustification", out var justElement) && justElement.ValueKind == JsonValueKind.String ? justElement.GetString() : "No justification provided.";
                            return new RankedProject { ProjectName = name ?? "Unknown Project", RelevanceJustification = justification ?? "No justification provided." };
                        })
                        .Where(rp => rp != null) // Filter out any nulls from failed parsing
                        .ToList();
                }
                else
                {
                    analysisResult.RankedProjects = new List<RankedProject>();
                     _logger.LogWarning("LLM response missing or invalid 'rankedProjects' array.");
                }

                // Safely extract summary string
                if (root.TryGetProperty("overallSummary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.String)
                {
                    analysisResult.OverallSummary = summaryElement.GetString() ?? "No summary provided.";
                }
                else
                {
                     analysisResult.OverallSummary = "Unable to generate summary.";
                     _logger.LogWarning("LLM response missing or invalid 'overallSummary' string.");
                }

                // 7. Return the structured analysis result
                return Ok(analysisResult);

            }
            catch (JsonException parseEx)
            {
                _logger.LogError(parseEx, "Failed to parse LLM JSON response for comparative analysis. Raw response was: {RawResponse}", rawLlmResponse);
                // Return a user-friendly error indicating the format was unexpected
                return StatusCode(500, new { message = "Failed to interpret AI analysis response. The format was not as expected.", rawResponsePreview = rawLlmResponse.Substring(0, Math.Min(200, rawLlmResponse.Length)) + "..." });
            }
            // --- END ROBUST PARSING ---

        }
        catch (Exception ex) // Catch general exceptions for the entire endpoint
        {
            _logger.LogError(ex, "Error performing comparative analysis for user {GitHubLogin}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { message = "An internal server error occurred while performing the analysis." });
        }
    }
    // --- END MODIFIED COMPARE PORTFOLIO ENDPOINT ---

    // --- DTOs ---
    public class GenerateRequest
    {
        public string Owner { get; set; } = string.Empty;
        public string RepoName { get; set; } = string.Empty;
    }

    public class CoverLetterRequest
    {
        public string Owner { get; set; } = string.Empty;
        public List<string> RepoNames { get; set; } = new List<string>();
        public string PositionRequirements { get; set; } = string.Empty; // This will be the job description
        // We can add more fields later if needed, like company name, position name
    }

    // --- DTOs for Comparative Analysis ---
    public class ComparePortfolioRequest
    {
        public string JobDescription { get; set; } = string.Empty;
    }

    // Enhanced DTO to hold richer data for the LLM prompt
    public class RepoAnalysisDetails
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Languages { get; set; } = new List<string>();
        public List<string> Topics { get; set; } = new List<string>();
        public string CustomTitle { get; set; } = string.Empty;
        public string CustomDescription { get; set; } = string.Empty;
        public List<string> CustomBulletPoints { get; set; } = new List<string>();
        public string ReadmeContent { get; set; } = string.Empty;
    }

    // Updated Result DTO to match the new, richer analysis structure
    public class ComparativeAnalysisResult
    {
        public List<string> IdentifiedJobSkills { get; set; } = new List<string>(); // NEW FIELD
        public List<string> MatchedSkills { get; set; } = new List<string>();
        public List<string> MissingSkills { get; set; } = new List<string>();
        public List<RankedProject> RankedProjects { get; set; } = new List<RankedProject>(); // CHANGED STRUCTURE
        public string OverallSummary { get; set; } = string.Empty; // NEW FIELD
    }

    public class RankedProject // NEW CLASS
    {
        public string ProjectName { get; set; } = string.Empty;
        public string RelevanceJustification { get; set; } = string.Empty; // NEW FIELD
    }
}