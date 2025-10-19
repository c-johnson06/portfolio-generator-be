using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PortfolioGenerator.Data;
using Microsoft.AspNetCore.HttpOverrides;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

Env.Load();

AppContext.SetSwitch("Npgsql.EnableLegacyIPv6Behavior", true);

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

var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("❌ Database connection string not found in environment variables or configuration.");

Console.WriteLine($"📡 Using connection string: {connectionString}");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
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
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    
    // CRITICAL FIX: Return 401 for API requests instead of redirecting
    options.Events.OnRedirectToLogin = context =>
    {
        // Check if this is an API request
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        
        // For non-API requests, allow the redirect
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
})
.AddGitHub(options =>
{
    options.ClientId = githubClientID;
    options.ClientSecret = githubClientSecret;
    options.CallbackPath = "/signin-github";
    options.SaveTokens = true;
    options.Scope.Add("read:user");
    options.Scope.Add("repo");
    
    options.Events.OnRemoteFailure = context =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var errorDescription = context.Request.Query["error_description"].FirstOrDefault() ?? "Unknown error";
        logger.LogError("GitHub OAuth failure: {Failure}", errorDescription);
        
        // Redirect to frontend with error
        context.Response.Redirect($"https://portfolio-generator-fe-five.vercel.app/?error={Uri.EscapeDataString(errorDescription)}");
        context.HandleResponse();
        return Task.CompletedTask;
    };
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://portfolio-generator-fe-five.vercel.app")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("Set-Cookie");
    });
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});

Console.WriteLine($"Loaded connection string: {builder.Configuration.GetConnectionString("DefaultConnection")}");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => "OK");

app.MapGet("/migrate", async (ApplicationDbContext db, ILogger<Program> logger) =>
{
    logger.LogInformation("Running migrations...");
    await db.Database.MigrateAsync();
    logger.LogInformation("✅ Migrations completed.");
    return Results.Text("✅ Success! Tables created in Supabase.", "text/plain");
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();