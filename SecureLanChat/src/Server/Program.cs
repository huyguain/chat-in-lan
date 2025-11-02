using Microsoft.EntityFrameworkCore;
using SecureLanChat.Data;
using SecureLanChat.Services;
using SecureLanChat.Hubs;
using SecureLanChat.Interfaces;
using SecureLanChat.Middleware;
using Serilog;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTPS
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        // Use the development certificate automatically
        // This ensures HTTPS works properly with the dev certificate
    });
});

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
    options.StreamBufferCapacity = 10;
});

// Add Entity Framework
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add custom services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IAESEncryptionService, AESEncryptionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILoggingService, LoggingService>();
builder.Services.AddScoped<IKeyStorageService, KeyStorageService>();
builder.Services.AddScoped<IDatabaseSeedingService, DatabaseSeedingService>();

// Add HTTP context accessor for logging
builder.Services.AddHttpContextAccessor();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Allow any origin
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Allow credentials for SignalR
    });
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("database", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Database is accessible"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPS redirection should be early in pipeline
app.UseHttpsRedirection();

// CORS should be before routing
app.UseCors("AllowAll");

// Serve static files from Client/wwwroot
// Try multiple possible paths
var baseDir = AppContext.BaseDirectory;
var currentDir = Directory.GetCurrentDirectory();
var possiblePaths = new[]
{
    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Client", "wwwroot")), // When running from bin/Debug/net8.0
    Path.GetFullPath(Path.Combine(currentDir, "..", "Client", "wwwroot")), // When running from src/Server
    Path.GetFullPath(Path.Combine(currentDir, "..", "..", "Client", "wwwroot")), // Alternative
    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "Client", "wwwroot")) // Another alternative
};

var clientPath = possiblePaths.FirstOrDefault(p => !string.IsNullOrEmpty(p) && Directory.Exists(p));

if (!string.IsNullOrEmpty(clientPath) && Directory.Exists(clientPath))
{
    Log.Information("Serving static files from: {ClientPath}", clientPath);
    var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(clientPath);
    
    // Configure default files (index.html, default.html, etc.)
    var defaultFilesOptions = new DefaultFilesOptions
    {
        FileProvider = fileProvider,
        RequestPath = ""
    };
    defaultFilesOptions.DefaultFileNames.Clear();
    defaultFilesOptions.DefaultFileNames.Add("index.html");
    app.UseDefaultFiles(defaultFilesOptions);
    
    // Configure static files
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        RequestPath = ""
    });
}
else
{
    Log.Warning("Client/wwwroot directory not found. Tried paths: {Paths}", string.Join(", ", possiblePaths));
    Log.Warning("Current directory: {CurrentDir}, Base directory: {BaseDir}", currentDir, baseDir);
    // Fallback to wwwroot if Client/wwwroot doesn't exist
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// Add middleware
app.UseRequestLogging();
app.UseGlobalExceptionMiddleware();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");
app.MapHealthChecks("/health");

// Fallback route to serve index.html for SPA
if (!string.IsNullOrEmpty(clientPath) && Directory.Exists(clientPath))
{
    app.MapFallback(async context =>
    {
        var indexPath = Path.Combine(clientPath, "index.html");
        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(indexPath);
        }
        else
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("index.html not found");
        }
    });
}

// Ensure database is created and seeded
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
    
    context.Database.EnsureCreated();
    await seedingService.SeedAsync();
}

try
{
    Log.Information("Starting Secure LAN Chat System");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
