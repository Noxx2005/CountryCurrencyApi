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

// **RAILWAY DATABASE SETUP**
string connectionString;

// Try multiple environment variable names
var connectionUrl = Environment.GetEnvironmentVariable("DATABASE_URL") ??
                   Environment.GetEnvironmentVariable("MYSQL_URL") ??
                   string.Empty;

Console.WriteLine($"🔍 DATABASE_URL value: '{connectionUrl}'");

if (string.IsNullOrEmpty(connectionUrl))
{
    // Fallback for local development
    Console.WriteLine("⚠️ DATABASE_URL not found. Using fallback local connection.");
    connectionString = "Server=localhost;Port=3306;Database=countries_db;Uid=root;Pwd=;SslMode=None;";
}
else
{
    Console.WriteLine($"🔗 Database URL found, length: {connectionUrl.Length}");

    // Use the direct connection string from Railway
    try
    {
        // If it's already in MySQL connection string format, use it directly
        if (connectionUrl.Contains("Server=") || connectionUrl.Contains("Host="))
        {
            connectionString = connectionUrl;
            Console.WriteLine("✅ Using direct MySQL connection string");
        }
        else
        {
            // Parse the mysql:// format
            var databaseUri = new Uri(connectionUrl);
            var userInfo = databaseUri.UserInfo.Split(':');

            var mysqlBuilder = new MySqlConnectionStringBuilder
            {
                Server = databaseUri.Host,
                Port = (uint)databaseUri.Port,
                Database = databaseUri.AbsolutePath.TrimStart('/'),
                UserID = userInfo[0],
                Password = userInfo.Length > 1 ? userInfo[1] : "",
                SslMode = MySqlSslMode.Required,
                AllowPublicKeyRetrieval = true
            };

            connectionString = mysqlBuilder.ToString();
            Console.WriteLine($"✅ Parsed Railway MySQL connection for database: {mysqlBuilder.Database}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Failed to parse connection string: {ex.Message}");
        // Use fallback
        connectionString = "Server=localhost;Port=3306;Database=countries_db;Uid=root;Pwd=;SslMode=None;";
    }
}

// Mask password for logging
var safeConnectionString = connectionString.Replace("Password=", "Password=***").Replace("Pwd=", "Pwd=***");
Console.WriteLine($"🔐 Using connection: {safeConnectionString}");

// Register DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Your other services
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

// Health check with database test
app.MapGet("/", () => "Country Currency API is running!");
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
                database = canConnect ? "Connected" : "Disconnected",
                timestamp = DateTime.UtcNow
            });
        }
        return Results.Json(new { status = "Unknown", database = "No Context", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            status = "Unhealthy",
            database = "Error",
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
        Console.WriteLine("🔄 Testing database connection...");
        var canConnect = await dbContext.Database.CanConnectAsync();

        if (canConnect)
        {
            Console.WriteLine("✅ Database connection successful!");
            await dbContext.Database.EnsureCreatedAsync();
            Console.WriteLine("✅ Database tables ready!");
        }
        else
        {
            Console.WriteLine("❌ Database connection failed - check connection string");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
}

Console.WriteLine("🎉 Application started successfully!");
app.Run();