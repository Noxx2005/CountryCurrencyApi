using CountryCurrencyApi.Data;
using CountryCurrencyApi.Services;
using CountryCurrencyApi.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Country Currency API",
        Version = "v1",
        Description = "A RESTful API that fetches country data from external APIs and provides CRUD operations",
        Contact = new OpenApiContact
        {
            Name = "Your Name",
            Email = "your.email@example.com"
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine($"Using connection string: {connectionString}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddScoped<ICountryService, CountryService>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddHttpClient();

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

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Country Currency API v1");
    c.RoutePrefix = "swagger"; // Swagger UI at /swagger
});


app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    Console.WriteLine("✅ Database created successfully!");

    var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "cache");
    if (!Directory.Exists(cacheDir))
        Directory.CreateDirectory(cacheDir);

    Console.WriteLine("✅ Cache directory ready");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database error: {ex.Message}");
    Console.WriteLine("💡 Make sure XAMPP MySQL is running on port 3306");
    throw;
}

Console.WriteLine("🚀 Application started successfully!");
Console.WriteLine("📚 Swagger UI available at: http://localhost:5000");
Console.WriteLine("🌐 API running at: " + string.Join(", ", app.Urls));

app.Run();
