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

// **PXXL PORT CONFIGURATION**
// PXXL typically uses port 8080 or provides a PORT env variable
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// **CLOUDCLUSTERS DATABASE SETUP**
string connectionString;

// Use environment variables (PXXL will set these)
var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
var dbPort = Environment.GetEnvironmentVariable("DB_PORT");

if (!string.IsNullOrEmpty(dbHost))
{
    // Use environment variables from PXXL
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
    Console.WriteLine("✅ Using CloudClusters MySQL with PXXL environment variables");
}
else
{
    // Fallback for local development
    Console.WriteLine("⚠️ Using local development database");
    connectionString = "Server=localhost;Port=3306;Database=countries_db;Uid=root;Pwd=;SslMode=None;";
}

Console.WriteLine($"🔐 Database configured successfully");

// Register DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Your services
builder.Services.AddScoped<ICountryService, CountryService>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddHttpClient();

// CORS - Configure for PXXL domain
builder.Services.AddCors(options =>
{
    options.AddPolicy("PXXLPolicy", policy =>
    {
        policy.WithOrigins(
                "https://*.pxxl.app",
                "https://your-app-name.pxxl.app"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });

    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// **PXXL SPECIFIC MIDDLEWARE**
// Health check endpoint required by many hosting providers
app.MapGet("/health", () => new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
});

// Use CORS
app.UseCors("AllowAll");

// Swagger - only in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // In production, you might want to protect Swagger or disable it
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Country Currency API v1");
        options.RoutePrefix = "api-docs"; // Access via /api-docs instead of root
    });
}

// Your existing middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthorization();
app.MapControllers();

// Root endpoint
app.MapGet("/", () => new
{
    message = "Country Currency API is running on PXXL!",
    version = "1.0.0",
    database = "CloudClusters MySQL",
    timestamp = DateTime.UtcNow
});

// Database initialization
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<AppDbContext>();

    if (dbContext != null)
    {
        Console.WriteLine("🔄 Testing database connection on PXXL...");
        var canConnect = await dbContext.Database.CanConnectAsync();

        if (canConnect)
        {
            Console.WriteLine("✅ Database connection successful on PXXL!");
            await dbContext.Database.EnsureCreatedAsync();
            Console.WriteLine("✅ Database tables ready!");
        }
        else
        {
            Console.WriteLine("❌ Database connection failed on PXXL");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"💥 Database setup failed on PXXL: {ex.Message}");
}

Console.WriteLine($"🚀 Application starting on port {port}");
app.Run();