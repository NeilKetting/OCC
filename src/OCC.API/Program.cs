using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OCC.API.Data;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "log-.txt");
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 2)
    .CreateLogger();

builder.Host.UseSerilog();

var environment = builder.Environment.EnvironmentName;
Console.WriteLine($"[STARTUP] ------------------------------------------------");
Console.WriteLine($"[STARTUP] ASPNETCORE_ENVIRONMENT: {environment}");
Console.WriteLine($"[STARTUP] ------------------------------------------------");

// Always load appsettings.secrets.json if it exists (for local secrets or production overrides)
builder.Configuration.AddJsonFile("appsettings.secrets.json", optional: true, reloadOnChange: true);

// Add services to the container.
// Add services to the container.
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<OCC.API.Infrastructure.Filters.ConcurrencyExceptionFilter>();
        options.Filters.Add<OCC.API.Infrastructure.Filters.SuppressRowVersionFilter>();
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddHttpContextAccessor();

// Database
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var httpContext = serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    
    // Check for environment header OR query parameter
    var environmentHeader = httpContext?.Request.Headers["X-Environment"].ToString();
    var environmentQuery = httpContext?.Request.Query["env"].ToString();
    var selectedEnv = !string.IsNullOrEmpty(environmentHeader) ? environmentHeader : environmentQuery;
    
    string connectionString;
    if (selectedEnv == "Test")
    {
        connectionString = configuration.GetConnectionString("TestConnection") 
                           ?? configuration.GetConnectionString("DefaultConnection")!;
    }
    else
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
        sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });
});

// (Optional) Add DbInitializer if you want to use it as a service, 
// but usually we call it in the app scope below.

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // Handle SignalR authentication via Query String
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// SignalR
builder.Services.AddSignalR();

// Email Service (Mock/Local for Dev)
builder.Services.AddSingleton<OCC.API.Services.IEmailService, OCC.API.Services.MockEmailService>();
// Security
builder.Services.AddScoped<OCC.API.Services.PasswordHasher>();
builder.Services.AddScoped<OCC.API.Services.IAuthService, OCC.API.Services.AuthService>();
builder.Services.AddScoped<OCC.API.Services.IStockService, OCC.API.Services.StockService>();
builder.Services.AddScoped<OCC.API.Services.INotificationService, OCC.API.Services.NotificationService>();
builder.Services.AddHostedService<OCC.API.Services.DatabaseBackupService>();
builder.Services.AddHostedService<OCC.API.Services.AutoClockInService>();
builder.Services.AddHostedService<OCC.API.Services.SignalRHeartbeatService>();

// OpenAPI (Built-in .NET 10)
builder.Services.AddOpenApi();

    // NUCLEAR OPTION: Hardcode the key temporarily to bypass the file system entirely
    try
    {
        var json = @"
{
  ""type"": ""service_account"",
  ""project_id"": ""occ-erp"",
  ""private_key_id"": ""c0b8a5ac02a8c3d292c6a06cc083c6086a4edf1e"",
  ""private_key"": ""-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDLL6hPJF+CZkcF\nOoFYmPMQ+c/64N01CmsYaxClAF6PwlbULX7zB9KsBzizr7UjT+auul4uYk01CDZO\npw6G0saXkIsCp0nhC8yrc0D+w8p+wk8nU49pbqDZ8ulQHYTzzy0IhNp24S4OJbwf\n97/8gfyE/Cy2R7RBdyWzEzjQnxvsDiNrF9FzO3Y6bZGnNpzGzW21RXnzD62ALEyN\nNPmQPrcx7ZVmo0UZuevpfEgrCBG71cqdscAwyqP2q9I+ITxSxYhb/KDsVLWbxvEe\ngn18WVKXwZamT0KHLLjahruTbpiDWuD1SA+yiRsIhsQjxIQy+qlFbD5VUolDTuLK\nvyj52MoLAgMBAAECggEAECne5Eja9jcnsDFKx98G+xM8adNIlacaBOvDe7TPUPVf\nTeq+nhvBtSCv8I9qRABftAesZVk5lh3soA4nGC+dT8JWZKQlOuti4UK+aWXu7m2L\nuW+qyXLdBemOiOqIQJL7HKHg9TMNpF95GzvswGwgx/19mxSSMOEHFTtSujnmET2c\nX/maZo10MwFoLj9LCFk3XF7INL1kT50ONTRzxMjybKPg1c+MmVYNeq1JyA4lC5/c\nu1GyJ/WFyyROGkScLY4X+lcX7EX83St+b0K9ncVYviZJ6KFfsP0rKRnBU6GFZ8he\OdilzAlS8exFCtxrmjeiof2sw/GCh9XXK6GlUT1fPQKBgQDtUc7S0Cys2XPOaC4L\nVOTlfDv81KyRbyMmN7q9KqrokRmI4Um9IDURhYHvglCPBxFWNpvMFji8uWf0e1q0\nEJBtMW3kfX98PJDdNXFqUIdubImbHLXnr7aY7F4waYk94WB6MZ5OujIu21wkJlhV\nJ4PpZwzme5IilOr15KKalGYG5wKBgQDbLghIW56fUub+/1K/3Xs+EpUJW0uDVJvB\nooM/Z62gDKs4ghcMuyZ8UmkNPnxJfNGYzcnxXvZIVT9Jox41dl3JXQhA+CFNi65m\ndxCXA0++ClFkAjthQaEuVDL7S+6Va0N9wjiDuq82EJPtvRMWb/Er3Xk1QGNzKTqB\nqJ5jSgoTPQKBgHloC42Pj/tRN0xVwZBserjnyGx8hFfWaj3n7rFNfaeCa3S6BBYr\nvtpa2XEk0n+JFxZq02Mhzx7FHuhUnr9VZf1mdxiYFzsAZP+1knLYBaC5B+CBXJHN\nM3WiHkFYDCzK+qcocRtHZ9rOv6GCuFe/4lzqKhBTERx94IGw2HqKBnPrAoGAa1PT\nQnt65VHXQ68LemCeZPr8eCR4icr4qo1F79p5LxKFFZq+ZsGOSvqf7phWjDXO/SBo\nbwWtXCZCY3C47j0UF/Kyg/39cNehgxNy0EAS4GB1Ep/1K97TarhYbq30Gr73wbFF\ns1vLSJI9ngEkQ6x1UKGXJPhuuonJ2IwVY1FyNZECgYEArUENgy875hiUrSHs5Smc\nfuSvA9KaYHDXBJv2hPqjyygiIsL66+q2PVmvK5HhCA4p01aDTiFj4KUrItaDVduy\nAYcqfUY83QgQhJA07mvWZxvuUh+LLSq87xczP5zrEJOuwOOrlU1kGUOvhxcw6q7v\n84CN6ymQSc+ioQsKpS2X4ME=\n-----END PRIVATE KEY-----\n"",
  ""client_email"": ""firebase-adminsdk-fbsvc@occ-erp.iam.gserviceaccount.com"",
  ""client_id"": ""112180666520112396745"",
  ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
  ""token_uri"": ""https://oauth2.googleapis.com/token"",
  ""auth_provider_x509_cert_url"": ""https://www.googleapis.com/oauth2/v1/certs"",
  ""client_x509_cert_url"": ""https://www.googleapis.com/robot/v1/metadata/x509/firebase-adminsdk-fbsvc%40occ-erp.iam.gserviceaccount.com"",
  ""universe_domain"": ""googleapis.com""
}
";

        FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions()
        {
            Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(json.Trim())
        });
        Console.WriteLine("[STARTUP] Firebase initialized successfully using HARDCODED key.");
    }
catch (Exception ex)
{
    OCC.API.Controllers.NotificationsController.FirebaseInitError = ex.Message;
    Console.WriteLine($"[CRITICAL] Failed to initialize Firebase: {ex.Message}");
}

var app = builder.Build();

// Seed Databases
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var configuration = services.GetRequiredService<IConfiguration>();
    var hasher = services.GetRequiredService<OCC.API.Services.PasswordHasher>();

    var connectionNames = new[] { "DefaultConnection", "TestConnection" };

    foreach (var connectionName in connectionNames)
    {
        try
        {
            var connectionString = configuration.GetConnectionString(connectionName);
            if (string.IsNullOrEmpty(connectionString)) 
            {
                logger.LogWarning($"Skipping {connectionName}: No connection string found.");
                continue;
            }

            logger.LogInformation($"[DB-INIT] Checking {connectionName}...");
            
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString);
            
            using var context = new AppDbContext(optionsBuilder.Options, services.GetRequiredService<IHttpContextAccessor>());
            
            DbInitializer.Initialize(context, hasher, app.Environment.IsDevelopment(), logger);
            
            logger.LogInformation($"[DB-INIT] {connectionName} is ready.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"[DB-INIT] Failed to initialize {connectionName}. Error: {ex.Message}");
        }
    }
}

// Enable OpenAPI in all environments for now
app.MapOpenApi();
// app.UseSwaggerUI(); // You can use alternatives like Scalar or others if needed

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseSerilogRequestLogging();

app.UseHttpMethodOverride();

app.UseAuthentication();
app.UseAuthorization();

// app.UseHttpMethodOverride(); // Removed from here

app.MapControllers();
app.MapHub<OCC.API.Hubs.NotificationHub>("/hubs/notifications");
app.MapHub<OCC.API.Hubs.ChatHub>("/hubs/chat");

app.Run();
