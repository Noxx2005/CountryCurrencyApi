using CountryCurrencyApi.Data;
using CountryCurrencyApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Imaging;

namespace CountryCurrencyApi.Services
{
    public class ImageService : IImageService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ImageService> _logger;

        public ImageService(AppDbContext context, ILogger<ImageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task GenerateSummaryImageAsync()
        {
            try
            {
                var (totalCountries, lastRefreshed) = await GetStatusDataAsync();
                var topCountries = await GetTopCountriesByGdpAsync(5);

                int width = 600, height = 400;
                using var bitmap = new Bitmap(width, height);
                using var g = Graphics.FromImage(bitmap);

                // White background
                g.Clear(Color.White);

                // Fonts and brushes
                using var titleFont = new Font("Arial", 18, FontStyle.Bold);
                using var normalFont = new Font("Arial", 14);
                using var smallFont = new Font("Arial", 12);
                using var blackBrush = new SolidBrush(Color.Black);
                using var blueBrush = new SolidBrush(Color.DarkBlue);

                int y = 20;

                g.DrawString("Country Currency API Summary", titleFont, blackBrush, new PointF(20, y));
                y += 40;
                g.DrawString($"Total Countries: {totalCountries}", normalFont, blackBrush, new PointF(20, y));
                y += 30;
                g.DrawString($"Last Refreshed: {lastRefreshed:yyyy-MM-dd HH:mm:ss UTC}", normalFont, blackBrush, new PointF(20, y));
                y += 40;
                g.DrawString("Top 5 Countries by GDP:", titleFont, blackBrush, new PointF(20, y));
                y += 40;

                // Draw list
                int i = 1;
                foreach (var country in topCountries)
                {
                    var gdpText = country.EstimatedGdp?.ToString("N2") ?? "N/A";
                    g.DrawString($"{i}. {country.Name}", normalFont, blackBrush, new PointF(30, y));
                    g.DrawString($"GDP: ${gdpText}", smallFont, blueBrush, new PointF(30, y + 20));
                    y += 40;
                    i++;
                }

                // Ensure cache directory exists
                var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "cache");
                if (!Directory.Exists(cacheDir))
                    Directory.CreateDirectory(cacheDir);

                var imagePath = Path.Combine(cacheDir, "summary.png");
                bitmap.Save(imagePath, ImageFormat.Png);

                _logger.LogInformation("Summary image generated successfully at {Path}", imagePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating summary image");
                // Do not throw — failure to generate image shouldn't affect refresh operation
            }
        }

        private async Task<(int totalCountries, DateTime lastRefreshed)> GetStatusDataAsync()
        {
            var totalCountries = await _context.Countries.CountAsync();
            var lastRefreshed = await _context.Countries
                .MaxAsync(c => c.LastRefreshedAt);
            return (totalCountries, lastRefreshed);
        }

        private async Task<List<Country>> GetTopCountriesByGdpAsync(int count)
        {
            return await _context.Countries
                .Where(c => c.EstimatedGdp != null)
                .OrderByDescending(c => c.EstimatedGdp)
                .Take(count)
                .ToListAsync();
        }
    }
}
