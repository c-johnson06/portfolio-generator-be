using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Octokit;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net;
using System.Reflection.Metadata.Ecma335;

[ApiController]
[Route("api/[controller]")]

public class UserController : ControllerBase
{
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
                user.PublicRepos
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
}