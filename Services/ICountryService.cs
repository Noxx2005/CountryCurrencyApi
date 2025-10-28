using CountryCurrencyApi.Models;

namespace CountryCurrencyApi.Services
{
    public interface ICountryService
    {
        Task RefreshCountriesAsync();
        Task<List<Country>> GetCountriesAsync(string? region = null, string? currency = null, string? sort = null);
        Task<Country?> GetCountryByNameAsync(string name);
        Task<bool> DeleteCountryAsync(string name);
        Task<(int totalCountries, DateTime? lastRefreshed)> GetStatusAsync();
    }
}