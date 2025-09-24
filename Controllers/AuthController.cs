using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login(string returnUrl = "https://localhost:3000/dashboard")
    {
        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        return Challenge(properties, "GitHub");
    }
}