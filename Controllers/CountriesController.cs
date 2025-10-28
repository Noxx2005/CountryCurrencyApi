using CountryCurrencyApi.Models;
using CountryCurrencyApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CountryCurrencyApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CountriesController : ControllerBase
    {
        private readonly ICountryService _countryService;
        private readonly ILogger<CountriesController> _logger;

        public CountriesController(ICountryService countryService, ILogger<CountriesController> logger)
        {
            _countryService = countryService;
            _logger = logger;
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshCountries()
        {
            try
            {
                await _countryService.RefreshCountriesAsync();
                return Ok(new { message = "Countries refreshed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing countries");
                return StatusCode(503, new { error = "External data source unavailable", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCountries([FromQuery] string? region, [FromQuery] string? currency, [FromQuery] string? sort)
        {
            try
            {
                var countries = await _countryService.GetCountriesAsync(region, currency, sort);
                return Ok(countries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting countries");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{name}")]
        public async Task<IActionResult> GetCountryByName(string name)
        {
            try
            {
                var country = await _countryService.GetCountryByNameAsync(name);
                if (country == null)
                    return NotFound(new { error = "Country not found" });

                return Ok(country);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting country by name");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpDelete("{name}")]
        public async Task<IActionResult> DeleteCountry(string name)
        {
            try
            {
                var deleted = await _countryService.DeleteCountryAsync(name);
                if (!deleted)
                    return NotFound(new { error = "Country not found" });

                return Ok(new { message = "Country deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting country");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("image")]
        public IActionResult GetCountriesImage()
        {
            var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "cache", "summary.png");
            if (!System.IO.File.Exists(imagePath))
                return NotFound(new { error = "Summary image not found" });

            var imageBytes = System.IO.File.ReadAllBytes(imagePath);
            return File(imageBytes, "image/png");
        }
    }
}