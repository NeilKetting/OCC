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

// Add CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add services to the container.
// Add services to the container.
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<OCC.API.Infrastructure.Filters.ConcurrencyExceptionFilter>();
        options.Filters.Add<OCC.API.Infrastructure.Filters.DatabaseExceptionFilter>();
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
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection")!;

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

// Email Service
var smtpSection = builder.Configuration.GetSection("SmtpSettings");
if (smtpSection.Exists() && !string.IsNullOrEmpty(smtpSection["Host"]))
{
    builder.Services.Configure<OCC.API.Services.SmtpSettings>(smtpSection);
    builder.Services.AddScoped<OCC.API.Services.IEmailService, OCC.API.Services.SmtpEmailService>();
    Console.WriteLine("[STARTUP] Registered SmtpEmailService.");
}
else
{
    builder.Services.AddSingleton<OCC.API.Services.IEmailService, OCC.API.Services.MockEmailService>();
    Console.WriteLine("[STARTUP] Registered MockEmailService (Fallback).");
}
// Security
builder.Services.AddScoped<OCC.API.Services.IPasswordHasher, OCC.API.Services.PasswordHasher>();
builder.Services.AddScoped<OCC.API.Services.PasswordHasher>();
builder.Services.AddScoped<OCC.API.Services.IAuthService, OCC.API.Services.AuthService>();
builder.Services.AddScoped<OCC.API.Services.IStockService, OCC.API.Services.StockService>();
builder.Services.AddScoped<OCC.API.Services.INotificationService, OCC.API.Services.NotificationService>();
builder.Services.AddHostedService<OCC.API.Services.DatabaseBackupService>();
builder.Services.AddHostedService<OCC.API.Services.AutoClockInService>();
builder.Services.AddHostedService<OCC.API.Services.SignalRHeartbeatService>();
builder.Services.AddHostedService<OCC.API.Services.AuditLogCleanupService>();

// Wage Calculation Engine
builder.Services.Configure<OCC.API.Services.WageCalculationOptions>(
    builder.Configuration.GetSection("WageCalculation"));
builder.Services.AddScoped<OCC.API.Services.IWageCalculationService, OCC.API.Services.WageCalculationService>();
builder.Services.AddScoped<OCC.API.Services.IWageRunService, OCC.API.Services.WageRunService>();

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

    var connectionName = "DefaultConnection";
    try
    {
        var connectionString = configuration.GetConnectionString(connectionName);
        if (string.IsNullOrEmpty(connectionString)) 
        {
            logger.LogWarning($"Skipping {connectionName}: No connection string found.");
        }
        else
        {
            logger.LogInformation($"[DB-INIT] Checking {connectionName}...");
            
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString);
            
            using var context = new AppDbContext(optionsBuilder.Options, services.GetRequiredService<IHttpContextAccessor>());
            
            DbInitializer.Initialize(context, hasher, app.Environment.IsDevelopment(), logger);
            
            logger.LogInformation($"[DB-INIT] {connectionName} is ready.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"[DB-INIT] Failed to initialize {connectionName}. Error: {ex.Message}");
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
app.UseMiddleware<OCC.API.Middleware.GlobalExceptionMiddleware>();

app.UseHttpMethodOverride();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// app.UseHttpMethodOverride(); // Removed from here

app.MapControllers();
app.MapHub<OCC.API.Hubs.NotificationHub>("/hubs/notifications");
app.MapHub<OCC.API.Hubs.ChatHub>("/hubs/chat");

app.Run();
