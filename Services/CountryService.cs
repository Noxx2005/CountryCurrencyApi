using CountryCurrencyApi.Data;
using CountryCurrencyApi.Models;
using CountryCurrencyApi.Models.ExternalApis;
using CountryCurrencyApi.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using System.Text.Json;

namespace CountryCurrencyApi.Services
{
    public class CountryService : ICountryService
    {
        private readonly AppDbContext _context;
        private readonly IExchangeRateService _exchangeRateService;
        private readonly IImageService _imageService;
        private readonly ILogger<CountryService> _logger;
        private readonly Random _random = new();

        public CountryService(AppDbContext context, IExchangeRateService exchangeRateService,
            IImageService imageService, ILogger<CountryService> logger)
        {
            _context = context;
            _exchangeRateService = exchangeRateService;
            _imageService = imageService;
            _logger = logger;
        }

        public async Task RefreshCountriesAsync()
        {
            try
            {
                // Fetch countries from external API
                var countries = await FetchCountriesFromApiAsync();

                // Fetch exchange rates
                var exchangeRates = await _exchangeRateService.GetExchangeRatesAsync();

                // Process and save countries
                await ProcessAndSaveCountriesAsync(countries, exchangeRates);

                // Generate summary image
                await _imageService.GenerateSummaryImageAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing countries");
                throw;
            }
        }

        private async Task<List<RestCountryResponse>> FetchCountriesFromApiAsync()
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.GetAsync(Utilities.Constants.RestCountriesUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<RestCountryResponse>>(json) ?? new List<RestCountryResponse>();
        }

        private async Task ProcessAndSaveCountriesAsync(List<RestCountryResponse> apiCountries,
            Dictionary<string, decimal> exchangeRates)
        {
            var existingCountries = await _context.Countries.ToListAsync();

            foreach (var apiCountry in apiCountries)
            {
                var currencyCode = GetCurrencyCode(apiCountry.Currencies);
                var exchangeRate = GetExchangeRate(currencyCode, exchangeRates);
                var estimatedGdp = CalculateEstimatedGdp(apiCountry.Population, exchangeRate);

                var existingCountry = existingCountries.FirstOrDefault(c =>
                    string.Equals(c.Name, apiCountry.Name, StringComparison.OrdinalIgnoreCase));

                if (existingCountry != null)
                {
                    // Update existing country
                    UpdateCountry(existingCountry, apiCountry, currencyCode, exchangeRate, estimatedGdp);
                }
                else
                {
                    // Create new country
                    var newCountry = CreateCountry(apiCountry, currencyCode, exchangeRate, estimatedGdp);
                    _context.Countries.Add(newCountry);
                }
            }

            await _context.SaveChangesAsync();
        }

        private static string? GetCurrencyCode(List<CurrencyResponse>? currencies)
        {
            return currencies?.FirstOrDefault()?.Code;
        }

        private decimal? GetExchangeRate(string? currencyCode, Dictionary<string, decimal> exchangeRates)
        {
            if (string.IsNullOrEmpty(currencyCode) || currencyCode == "USD")
                return 1;

            return exchangeRates.GetValueOrDefault(currencyCode);
        }

        private decimal? CalculateEstimatedGdp(long population, decimal? exchangeRate)
        {
            if (exchangeRate == null || exchangeRate == 0)
                return null;

            var randomMultiplier = _random.Next(1000, 2001);
            return (population * randomMultiplier) / exchangeRate;
        }

        private static void UpdateCountry(Country country, RestCountryResponse apiCountry,
            string? currencyCode, decimal? exchangeRate, decimal? estimatedGdp)
        {
            country.Capital = apiCountry.Capital;
            country.Region = apiCountry.Region;
            country.Population = apiCountry.Population;
            country.CurrencyCode = currencyCode ?? string.Empty;
            country.ExchangeRate = exchangeRate;
            country.EstimatedGdp = estimatedGdp;
            country.FlagUrl = apiCountry.Flag;
            country.LastRefreshedAt = DateTime.UtcNow;
            country.UpdatedAt = DateTime.UtcNow;
        }

        private static Country CreateCountry(RestCountryResponse apiCountry, string? currencyCode,
            decimal? exchangeRate, decimal? estimatedGdp)
        {
            return new Country
            {
                Name = apiCountry.Name,
                Capital = apiCountry.Capital,
                Region = apiCountry.Region,
                Population = apiCountry.Population,
                CurrencyCode = currencyCode ?? string.Empty,
                ExchangeRate = exchangeRate,
                EstimatedGdp = estimatedGdp,
                FlagUrl = apiCountry.Flag,
                LastRefreshedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public async Task<List<Country>> GetCountriesAsync(string? region = null, string? currency = null, string? sort = null)
        {
            var query = _context.Countries.AsQueryable();

            if (!string.IsNullOrEmpty(region))
                query = query.Where(c => c.Region == region);

            if (!string.IsNullOrEmpty(currency))
                query = query.Where(c => c.CurrencyCode == currency);

            query = sort?.ToLower() switch
            {
                "gdp_desc" => query.OrderByDescending(c => c.EstimatedGdp),
                "gdp_asc" => query.OrderBy(c => c.EstimatedGdp),
                "population_desc" => query.OrderByDescending(c => c.Population),
                "population_asc" => query.OrderBy(c => c.Population),
                "name_asc" => query.OrderBy(c => c.Name),
                "name_desc" => query.OrderByDescending(c => c.Name),
                _ => query.OrderBy(c => c.Name)
            };

            return await query.ToListAsync();
        }

        public async Task<Country?> GetCountryByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var normalizedName = name.Trim().ToLower();

            return await _context.Countries
                .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedName);
        }

        public async Task<bool> DeleteCountryAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var normalizedName = name.Trim().ToLower();
            var country = await _context.Countries
                .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedName);

            if (country == null)
                return false;

            _context.Countries.Remove(country);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(int totalCountries, DateTime? lastRefreshed)> GetStatusAsync()
        {
            var totalCountries = await _context.Countries.CountAsync();
            var lastRefreshed = await _context.Countries
                .MaxAsync(c => (DateTime?)c.LastRefreshedAt);

            return (totalCountries, lastRefreshed);
        }
    }
}