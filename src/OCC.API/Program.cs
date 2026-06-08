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

    // Initialize Firebase Admin SDK
    try
    {
        var keyPath = @"C:\OCC-Source\Keys\service-account.json";
        if (File.Exists(keyPath))
        {
            var json = File.ReadAllText(keyPath);
            FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions()
            {
                Credential = Google.Apis.Auth.OAuth2.CredentialFactory.FromJson<Google.Apis.Auth.OAuth2.ServiceAccountCredential>(json).ToGoogleCredential()
            });
            Console.WriteLine($"[STARTUP] Firebase initialized successfully from: {keyPath}");
        }
        else
        {
            Console.WriteLine($"[STARTUP] Firebase SKIPPED: File not found at {keyPath}");
        }
    }
catch (Exception ex)
{
    OCC.API.Controllers.NotificationsController.FirebaseInitError = $"{ex.Message} | {ex.StackTrace}";
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
