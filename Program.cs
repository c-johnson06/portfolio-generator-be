using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

var githubClientID = builder.Configuration["GitHub:ClientID"];
var githubClientSecret = builder.Configuration["GitHub:ClientSecret"];

if (string.IsNullOrEmpty(githubClientID) || string.IsNullOrEmpty(githubClientSecret))
{
    throw new InvalidOperationException("GitHub ClientID and ClientSecret must be provided in configuration.");
}

builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "GitHub";
})
.AddCookie()
.AddGitHub(options =>
{
    options.ClientId = githubClientID;
    options.ClientSecret = githubClientSecret;
    options.SaveTokens = true;

    options.Scope.Add("read:user");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
