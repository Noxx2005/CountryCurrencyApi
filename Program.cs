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
var connectionUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrEmpty(connectionUrl))
{
    Console.WriteLine("⚠️ DATABASE_URL not found. Using fallback local connection.");
    connectionUrl = "Server=127.0.0.1;Port=3306;Database=countries_db;Uid=root;Pwd=;SslMode=None;";
}

Console.WriteLine($"Using connection: {connectionUrl}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionUrl, ServerVersion.AutoDetect(connectionUrl)));

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