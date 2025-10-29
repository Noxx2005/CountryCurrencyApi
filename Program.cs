using CountryCurrencyApi.Data;
using CountryCurrencyApi.Services;
using CountryCurrencyApi.Middleware;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// **CLOUDCLUSTERS DATABASE SETUP**
string connectionString;

// Option 1: Use environment variables (recommended for Railway)
var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
var dbPort = Environment.GetEnvironmentVariable("DB_PORT");

// Option 2: Direct configuration for testing
if (string.IsNullOrEmpty(dbHost))
{
    Console.WriteLine("⚠️ Using direct CloudClusters configuration");

    var mysqlBuilder = new MySqlConnectionStringBuilder
    {
        Server = "mysql-204042-0.cloudclusters.net", // Your host
        Port = 10048, // Your port
        Database = "countrycurrency_db", // Your database name
        UserID = "Bot", // Your username
        Password = "asd12345", // Your password
        SslMode = MySqlSslMode.Required,
        AllowPublicKeyRetrieval = true,
        ConnectionTimeout = 30,
        DefaultCommandTimeout = 30
    };

    connectionString = mysqlBuilder.ToString();
}
else
{
    // Use environment variables
    var mysqlBuilder = new MySqlConnectionStringBuilder
    {
        Server = dbHost,
        Port = uint.Parse(dbPort ?? "10048"),
        Database = dbName ?? "countrycurrency_db",
        UserID = dbUser ?? "Bot",
        Password = dbPassword ?? "asd12345",
        SslMode = MySqlSslMode.Required,
        AllowPublicKeyRetrieval = true,
        ConnectionTimeout = 30,
        DefaultCommandTimeout = 30
    };

    connectionString = mysqlBuilder.ToString();
    Console.WriteLine("✅ Using CloudClusters MySQL with environment variables");
}

Console.WriteLine($"🔐 Using database: {connectionString.Replace("Password=asd12345", "Password=***").Replace("Pwd=asd12345", "Pwd=***")}");

// Register DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
        mysqlOptions =>
        {
            mysqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        });
});

// Your services
builder.Services.AddScoped<ICountryService, CountryService>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddHttpClient();

// CORS
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

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health check
app.MapGet("/", () => "Country Currency API with CloudClusters MySQL");
app.MapGet("/health", async (AppDbContext? dbContext) =>
{
    try
    {
        if (dbContext != null)
        {
            var canConnect = await dbContext.Database.CanConnectAsync();
            return Results.Json(new
            {
                status = canConnect ? "Healthy" : "Degraded",
                database = canConnect ? "CloudClusters Connected" : "Disconnected",
                timestamp = DateTime.UtcNow
            });
        }
        return Results.Json(new { status = "No Database Context", timestamp = DateTime.UtcNow });
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

// Database initialization
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<AppDbContext>();

    if (dbContext != null)
    {
        Console.WriteLine("🔄 Testing CloudClusters database connection...");

        // Test basic connection first
        var canConnect = await dbContext.Database.CanConnectAsync();

        if (canConnect)
        {
            Console.WriteLine("✅ CloudClusters database connection successful!");

            // Create tables if they don't exist
            await dbContext.Database.EnsureCreatedAsync();
            Console.WriteLine("✅ Database tables ready!");

            // Test a simple query
            var count = await dbContext.Countries.CountAsync();
            Console.WriteLine($"📊 Current countries in database: {count}");
        }
        else
        {
            Console.WriteLine("❌ CloudClusters database connection failed");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"💥 Database initialization error: {ex.Message}");
    Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
}

Console.WriteLine("🎉 Application started with CloudClusters MySQL!");
app.Run();