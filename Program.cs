using CountryCurrencyApi.Data;
using CountryCurrencyApi.Services;
using CountryCurrencyApi.Middleware;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// **PXXL SPECIFIC CONFIGURATION**
// PXXL uses port 8080 internally, but routes through their proxy
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// **DATABASE SETUP**
string connectionString;

var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
var dbPort = Environment.GetEnvironmentVariable("DB_PORT");

if (!string.IsNullOrEmpty(dbHost))
{
    var mysqlBuilder = new MySqlConnectionStringBuilder
    {
        Server = dbHost,
        Port = uint.Parse(dbPort ?? "10048"),
        Database = dbName ?? "countrycurrency_db",
        UserID = dbUser ?? "Bot",
        Password = dbPassword ?? "asd12345",
        SslMode = MySqlSslMode.Required,
        AllowPublicKeyRetrieval = true
    };

    connectionString = mysqlBuilder.ToString();
    Console.WriteLine("✅ Using CloudClusters MySQL");
}
else
{
    connectionString = "Server=localhost;Port=3306;Database=countries_db;Uid=root;Pwd=;SslMode=None;";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Services
builder.Services.AddScoped<ICountryService, CountryService>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddHttpClient();

// CORS for PXXL
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// **PXXL ROUTING FIXES**

// Always use Swagger in production for PXXL
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Country Currency API v1");
    c.RoutePrefix = "swagger"; // Changed from "api-docs" to "swagger"
});

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors("AllowAll");
app.UseRouting(); // ← ADD THIS LINE
app.UseAuthorization();
app.MapControllers();

// **EXPLICIT ROUTE MAPPING FOR PXXL**
app.MapGet("/", () => new
{
    message = "Country Currency API is running on PXXL!",
    status = "Healthy",
    timestamp = DateTime.UtcNow,
    endpoints = new[] {
        "/health",
        "/swagger",
        "/api/Countries",
        "/api/Countries/refresh"
    }
});

app.MapGet("/health", async (AppDbContext? dbContext) =>
{
    try
    {
        var dbStatus = "Unknown";
        if (dbContext != null)
        {
            var canConnect = await dbContext.Database.CanConnectAsync();
            dbStatus = canConnect ? "Connected" : "Disconnected";
        }

        return Results.Json(new
        {
            status = "Healthy",
            database = dbStatus,
            timestamp = DateTime.UtcNow,
            environment = app.Environment.EnvironmentName
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            status = "Unhealthy",
            error = ex.Message,
            timestamp = DateTime.UtcNow
        }, statusCode: 503);
    }
});

// Test endpoint - always works
app.MapGet("/test", () => "PXXL Test Endpoint Working!");

// Database initialization
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<AppDbContext>();
    if (dbContext != null)
    {
        Console.WriteLine("🔄 Testing database connection...");
        await dbContext.Database.EnsureCreatedAsync();
        Console.WriteLine("✅ Database ready!");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database init: {ex.Message}");
}

Console.WriteLine($"🚀 Application started on port {port}");
app.Run();