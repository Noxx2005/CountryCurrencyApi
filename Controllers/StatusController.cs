using CountryCurrencyApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CountryCurrencyApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly ICountryService _countryService;

        public StatusController(ICountryService countryService)
        {
            _countryService = countryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var (totalCountries, lastRefreshed) = await _countryService.GetStatusAsync();
                return Ok(new
                {
                    total_countries = totalCountries,
                    last_refreshed_at = lastRefreshed?.ToString("yyyy-MM-ddTHH:mm:ssZ")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}