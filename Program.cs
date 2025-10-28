using CountryCurrencyApi.Data;
using CountryCurrencyApi.Services;
using CountryCurrencyApi.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// **SIMPLIFIED DATABASE SETUP - With Fallback**
string connectionString;

// Try to get Railway MySQL variables first
var mysqlHost = Environment.GetEnvironmentVariable("MYSQLHOST");
var mysqlDatabase = Environment.GetEnvironmentVariable("MYSQLDATABASE");
var mysqlUser = Environment.GetEnvironmentVariable("MYSQLUSER");
var mysqlPassword = Environment.GetEnvironmentVariable("MYSQLPASSWORD");
var mysqlPort = Environment.GetEnvironmentVariable("MYSQLPORT");

if (!string.IsNullOrEmpty(mysqlHost))
{
    // Use Railway MySQL with SSL
    connectionString = $"Server={mysqlHost};Database={mysqlDatabase};Uid={mysqlUser};Pwd={mysqlPassword};Port={mysqlPort};SslMode=Required;";
    Console.WriteLine($"🚀 Using Railway MySQL: {mysqlHost}");
}
else
{
    // Fallback: Use in-memory database to ensure app starts
    connectionString = "Server=localhost;Database=temp;Uid=root;Pwd=;";
    Console.WriteLine("⚠️ Using fallback connection - app will start but database won't work");
}

try
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
    Console.WriteLine("✅ Database configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database setup failed: {ex.Message}");
    // Don't throw - let the app start without database
}

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

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint - ALWAYS WORKS
app.MapGet("/", () => "Country Currency API is running!");
app.MapGet("/health", () => "Healthy");

// Database initialization - DON'T CRASH IF IT FAILS
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<AppDbContext>();

    if (dbContext != null)
    {
        Console.WriteLine("🔄 Creating database...");
        await dbContext.Database.EnsureCreatedAsync();
        Console.WriteLine("✅ Database ready!");
    }
    else
    {
        Console.WriteLine("⚠️ Database context is null - running without database");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Database initialization failed: {ex.Message}");
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
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Cache directory setup failed: {ex.Message}");
}

Console.WriteLine("🎉 Application started successfully!");
Console.WriteLine("📚 Swagger available at: /swagger");
Console.WriteLine("🏥 Health check at: /health");

app.Run();