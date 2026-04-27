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
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        }));

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

// --- INITIALIZE FIREBASE ---
try
{
    FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions()
    {
        Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile("service-account.json")
    });
}
catch (Exception ex)
{
    Console.WriteLine($"[CRITICAL] Failed to initialize Firebase: {ex.Message}");
    // Don't throw, let the app start so other features still work
}

var app = builder.Build();

// Configure the HTTP request pipeline.
// Configure the HTTP request pipeline.
// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Starting Database Initialization...");
        var context = services.GetRequiredService<AppDbContext>();
        
        // Log connection string (masked)
        var conn = context.Database.GetConnectionString();
        logger.LogInformation($"Using Connection String: {conn?.Split(';')[0]}... (Length: {conn?.Length})");

        var hasher = services.GetRequiredService<OCC.API.Services.PasswordHasher>();
        
        logger.LogInformation("Calling DbInitializer.Initialize()...");
        DbInitializer.Initialize(context, hasher, app.Environment.IsDevelopment(), logger);
        logger.LogInformation("Database Initialization Completed Successfully.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "FATAL ERROR: Parsing Database Migration failed.");
        if (ex.InnerException != null)
        {
             logger.LogCritical(ex.InnerException, "Inner Exception detected.");
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
