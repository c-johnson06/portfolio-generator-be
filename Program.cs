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

    if (builder.Environment.IsDevelopment())
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    }
    else
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }
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
        logger.LogError("Remote failure: {Failure}", errorDescription);
        return Task.CompletedTask;
    };
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMyFrontend", builder =>
    {
        builder.WithOrigins("https://localhost:3000")
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
    });
});

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var app = builder.Build();
Console.WriteLine($"Loaded connection string: {builder.Configuration.GetConnectionString("DefaultConnection")}");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseCors("AllowMyFrontend");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

app.MapGet("/migrate", async (ApplicationDbContext db, ILogger<Program> logger) =>
{
    logger.LogInformation("Running migrations...");
    await db.Database.MigrateAsync();
    logger.LogInformation("Migrations completed.");
    return Results.Text("✅ Success! Tables created in Supabase.", "text/plain");
});

app.Run();