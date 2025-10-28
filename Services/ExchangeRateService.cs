using CountryCurrencyApi.Models.ExternalApis;
using CountryCurrencyApi.Utilities;
using System.Text.Json;

namespace CountryCurrencyApi.Services
{
    public class ExchangeRateService : IExchangeRateService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ExchangeRateService> _logger;

        public ExchangeRateService(IHttpClientFactory httpClientFactory, ILogger<ExchangeRateService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<Dictionary<string, decimal>> GetExchangeRatesAsync()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Use the fully qualified name to avoid ambiguity
                var response = await httpClient.GetAsync(CountryCurrencyApi.Utilities.Constants.ExchangeRatesUrl);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var exchangeRateData = JsonSerializer.Deserialize<ExchangeRateResponse>(json);

                return exchangeRateData?.Rates ?? new Dictionary<string, decimal>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching exchange rates");
                throw new Exception("Could not fetch data from exchange rates API", ex);
            }
        }
    }
}