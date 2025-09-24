using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = "https://localhost:3000/dashboard"    
        };
        return Challenge(properties, "GitHub");
    }
}