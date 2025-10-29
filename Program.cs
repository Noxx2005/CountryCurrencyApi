using CountryCurrencyApi.Data;
using CountryCurrencyApi.Services;
using CountryCurrencyApi.Middleware;
using Microsoft.EntityFrameworkCore;
using MySqlConnector; // Make sure this is included

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// **DATABASE SETUP FOR RAILWAY**
var connectionUrl = Environment.GetEnvironmentVariable("DATABASE_URL") ??
                   Environment.GetEnvironmentVariable("MYSQL_URL") ??
                   Environment.GetEnvironmentVariable("MYSQLCONNSTR_localdb") ??
                   string.Empty;

string connectionString;

if (string.IsNullOrEmpty(connectionUrl))
{
    Console.WriteLine("⚠️ DATABASE_URL not found. Using fallback local connection.");
    connectionString = "Server=127.0.0.1;Port=3306;Database=countries_db;Uid=root;Pwd=;SslMode=None;";
}
else
{
    Console.WriteLine($"🔗 DATABASE_URL found: {connectionUrl}");

    // Parse the Railway MySQL URL format
    try
    {
        var databaseUri = new Uri(connectionUrl);
        var userInfo = databaseUri.UserInfo.Split(':');

        var mysqlBuilder = new MySqlConnectionStringBuilder
        {
            Server = databaseUri.Host,
            Port = (uint)(databaseUri.Port > 0 ? databaseUri.Port : 3306),
            Database = databaseUri.AbsolutePath.TrimStart('/'),
            UserID = userInfo[0],
            Password = userInfo.Length > 1 ? userInfo[1] : "",
            SslMode = MySqlSslMode.Required,
            // Additional options for better compatibility
            AllowPublicKeyRetrieval = true,
            ConnectionTimeout = 30,
            DefaultCommandTimeout = 30
        };

        connectionString = mysqlBuilder.ToString();
        Console.WriteLine($"✅ Parsed connection string successfully");
        Console.WriteLine($"🔧 Using database: {mysqlBuilder.Database} on {mysqlBuilder.Server}:{mysqlBuilder.Port}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Failed to parse DATABASE_URL: {ex.Message}");
        Console.WriteLine($"💡 Falling back to raw connection URL");
        // Try using the connection URL directly with some modifications
        connectionString = connectionUrl.Replace("mysql://", "Server=")
                                      .Replace("@", ";UserID=")
                                      .Replace(":", ";Password=")
                                      .Replace("/", ";Database=") + ";SslMode=Required;AllowPublicKeyRetrieval=true;";
    }
}

Console.WriteLine($"🔐 Final connection string: {connectionString.Replace("Password=", "Password=***").Replace("Pwd=", "Pwd=***")}");

// Register DbContext with retry logic for production
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

    // Only enable sensitive data logging in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Services with null checks
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

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("🚀 Running in Development mode");
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    Console.WriteLine("🚀 Running in Production mode");
    // In production, you might want to restrict Swagger
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Country Currency API v1");
        options.RoutePrefix = "swagger"; // You can set this to string.Empty to make Swagger UI available at root
    });
}

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint - ALWAYS WORKS
app.MapGet("/", () => new
{
    message = "Country Currency API is running!",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
});

app.MapGet("/health", async (AppDbContext? dbContext) =>
{
    try
    {
        var healthInfo = new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            database = "Unknown"
        };

        if (dbContext != null)
        {
            var canConnect = await dbContext.Database.CanConnectAsync();
            healthInfo = new
            {
                status = canConnect ? "Healthy" : "Degraded",
                timestamp = DateTime.UtcNow,
                database = canConnect ? "Connected" : "Disconnected"
            };
        }

        return Results.Json(healthInfo);
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            status = "Unhealthy",
            timestamp = DateTime.UtcNow,
            database = "Error",
            error = ex.Message
        }, statusCode: 503);
    }
});

// Database initialization - DON'T CRASH IF IT FAILS
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<AppDbContext>();

    if (dbContext != null)
    {
        Console.WriteLine("🔄 Testing database connection...");

        // Test connection first
        var canConnect = await dbContext.Database.CanConnectAsync();

        if (canConnect)
        {
            Console.WriteLine("✅ Database connection successful!");

            Console.WriteLine("🔄 Creating database if not exists...");
            await dbContext.Database.EnsureCreatedAsync();
            Console.WriteLine("✅ Database tables ready!");

            // You can add initial data seeding here if needed
            // await SeedData.Initialize(dbContext);
        }
        else
        {
            Console.WriteLine("❌ Database connection failed - application will run with limited functionality");
        }
    }
    else
    {
        Console.WriteLine("⚠️ Database context is null - running without database functionality");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
    Console.WriteLine($"🔍 Exception type: {ex.GetType().Name}");
    Console.WriteLine("💡 App will run without database functionality");
}

// Ensure cache directory exists
try
{
    var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "cache");
    if (!Directory.Exists(cacheDir))
    {
        Directory.CreateDirectory(cacheDir);
        Console.WriteLine("✅ Cache directory created");
    }
    else
    {
        Console.WriteLine("✅ Cache directory exists");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Cache directory setup failed: {ex.Message}");
}

// Log startup information
Console.WriteLine("🎉 Application started successfully!");
Console.WriteLine($"🌍 Environment: {app.Environment.EnvironmentName}");
Console.WriteLine("📚 Swagger available at: /swagger");
Console.WriteLine("🏥 Health check at: /health");
Console.WriteLine("🔍 Detailed health check at: /health with database status");

app.Run();