namespace BitfinexAPI.Interfaces
{
    public interface IClientRestAPI
    {
        Task<string?> GetTradesAsync(string symbol, Dictionary<string, string?> histParameters);
        Task<string?> GetCandlesAsync(string candle, Dictionary<string, string?> histParameters);
        Task<string?> GetTickersAsync(string symbol);
    }
}
