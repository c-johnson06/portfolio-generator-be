using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PortfolioGenerator.Data;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

Env.Load();

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

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.CommandTimeout(180);
            npgsqlOptions.EnableRetryOnFailure();
        })
);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "GitHub";
})
.AddCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
})
.AddGitHub(options =>
{
    options.ClientId = githubClientID;
    options.ClientSecret = githubClientSecret;
    options.SaveTokens = true;
    options.Scope.Add("read:user");
    options.Scope.Add("repo");
    options.Events.OnRemoteFailure = context =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var errorDescription = context.Request.Form["error_description"].FirstOrDefault() ?? "Unknown error";
        logger.LogError("GitHub OAuth failure: {Failure}", errorDescription);
        return Task.CompletedTask;
    };
});

// Allow frontend + Render domain for CORS
var frontendUrl = builder.Configuration["FRONTEND_URL"] ?? "https://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(frontendUrl, "https://portfolio-generator-fbbp.onrender.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

Console.WriteLine($"Loaded connection string: {builder.Configuration.GetConnectionString("DefaultConnection")}");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ✅ Health check (no CORS needed — simple GET)
app.MapGet("/health", () => "OK");

// ✅ Migration endpoint — make it work without CORS issues
app.MapGet("/migrate", async (ApplicationDbContext db, ILogger<Program> logger) =>
{
    logger.LogInformation("Running migrations...");
    await db.Database.MigrateAsync();
    logger.LogInformation("✅ Migrations completed.");
    return Results.Text("✅ Success! Tables created in Supabase.", "text/plain");
});

// ✅ Bind to 0.0.0.0 (required by Render)
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();