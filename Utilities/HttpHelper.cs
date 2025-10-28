using System.Net.Http.Json;
using System.Text.Json;

namespace CountryCurrencyApi.Utils
{
    public class HttpHelper
    {
        private readonly HttpClient _client;
        private readonly ILogger<HttpHelper> _logger;

        public HttpHelper(ILogger<HttpHelper> logger)
        {
            _logger = logger;
            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

  
        public async Task<JsonElement?> GetJsonAsync(string url)
        {
            try
            {
                var response = await _client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch {Url} with {Status}", url, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                return JsonDocument.Parse(content).RootElement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching {Url}", url);
                return null;
            }
        }
    }
}
