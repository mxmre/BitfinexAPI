namespace BitfinexAPI.Interfaces
{
    public interface IClientRestAPI
    {
        Task<string?> GetTradesAsync(string symbol, Dictionary<string, string?>? parameters);
        Task<string?> GetCandlesAsync(string candle, string section, Dictionary<string, string?>? parameters);
        Task<string?> GetTickersAsync(string symbol);
    }
}
