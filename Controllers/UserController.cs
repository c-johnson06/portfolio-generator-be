using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Octokit;

[ApiController]
[Route("api/[controller]")]

public class UserController : ControllerBase
{
    [HttpGet("me")]
    [Authorize]

    public async Task<IActionResult> GetCurrentUser()
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

        try
        {
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
        catch (ApiException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
        
    }
}