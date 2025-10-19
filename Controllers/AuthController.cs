using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;
    
    public AuthController(ILogger<AuthController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var frontendUrl = _configuration["FRONTEND_URL"] ?? "https://portfolio-generator-fe-five.vercel.app";
        
        // Store the return URL in authentication properties
        var properties = new AuthenticationProperties
        {
            RedirectUri = string.IsNullOrEmpty(returnUrl) 
                ? $"{frontendUrl}/dashboard"
                : $"{frontendUrl}{returnUrl}",
            
            // Store items that will be available after authentication
            Items =
            {
                { "scheme", "GitHub" }
            }
        };
        
        _logger.LogInformation("Initiating GitHub OAuth login with redirect: {RedirectUri}", properties.RedirectUri);
        
        return Challenge(properties, "GitHub");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var frontendUrl = _configuration["FRONTEND_URL"] ?? "https://portfolio-generator-fe-five.vercel.app";
        
        await HttpContext.SignOutAsync();
        
        _logger.LogInformation("User logged out successfully");
        
        return Ok(new { message = "Logged out successfully", redirectUrl = frontendUrl });
    }

    [HttpGet("check")]
    public IActionResult CheckAuth()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        
        if (isAuthenticated)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            _logger.LogInformation("Auth check: User {Username} is authenticated", username);
            
            return Ok(new
            {
                authenticated = true,
                username = username,
                userId = nameIdentifier
            });
        }
        
        _logger.LogInformation("Auth check: No authenticated user");
        return Ok(new { authenticated = false });
    }
}