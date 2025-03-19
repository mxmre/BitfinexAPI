using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitfinexAPI.Interfaces;
using BitfinexAPI.TestHQ;
using Microsoft.AspNetCore.WebUtilities;
using static System.Collections.Specialized.BitVector32;

namespace BitfinexAPI
{
    public class BitfinexClientRestAPI : IClientRestAPI
    {
        protected static HttpClient _httpClient;
        protected static readonly string _baseUrl = "https://api-pub.bitfinex.com/v2/";

        static BitfinexClientRestAPI()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = new TimeSpan(TimeSpan.TicksPerSecond * 15);
        }
        public BitfinexClientRestAPI()
        {
            
        }


        protected Task<string?> GetInfoAsync(string url)
        {
            return Task<string?>.Factory.StartNew(() =>
            {
                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    return "invalid url";
                HttpRequestMessage request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(url),
                    Headers =
                    {
                        { "accept", "application/json" },
                    },
                };
                string? jsonBody = null;
                using (var response = _httpClient.SendAsync(request).Result)
                {
                    response.EnsureSuccessStatusCode();
                    jsonBody = response.Content.ReadAsStringAsync().Result;
                    
                }

                return jsonBody;
            }, TaskCreationOptions.AttachedToParent);
        }
        public Task<string?> GetCandlesAsync(string candle, string section, Dictionary<string, string?>? parameters)
        {
            string baseUrl = _baseUrl + "candles/" + candle + "/" +
                section;
            string formedUrl = (parameters is null ?
                baseUrl : QueryHelpers.AddQueryString(baseUrl, parameters));
            
            return GetInfoAsync(formedUrl);
        }
        public Task<string?> GetTickersAsync(string symbol)
        {
            string baseUrl = _baseUrl + "ticker/" + symbol;
            return GetInfoAsync(baseUrl);
        }

        public Task<string?> GetTradesAsync(string symbol, Dictionary<string, string?>? parameters)
        {
            string baseUrl = _baseUrl + "trades/" + symbol + "/hist";
            string formedUrl = (parameters is null ?
                baseUrl : QueryHelpers.AddQueryString(baseUrl, parameters));
            return GetInfoAsync(formedUrl);
        }
    }
}
