namespace CountryCurrencyApi.Services
{
    public interface IExchangeRateService
    {
        Task<Dictionary<string, decimal>> GetExchangeRatesAsync();
    }
}