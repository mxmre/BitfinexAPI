using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitfinexAPI.Interfaces;
using BitfinexAPI.TestHQ;
using Microsoft.AspNetCore.WebUtilities;

namespace BitfinexAPI
{
    class BitfinexClientRestAPI : IClientRestAPI
    {
        protected HttpClient _httpClient;
        public BitfinexClientRestAPI(HttpClient httpClient) 
        {
            _httpClient = httpClient;
            
        }


        protected Task<string?> GetInfoAsync(string addUrl, Dictionary<string, string?>? histParameters, string infoType)
        {
            return new Task<string?>(() =>
            {
                string baseUrl = "https://api-pub.bitfinex.com/v2/" + infoType + "/" + addUrl +
                (infoType == "ticker" ? "" : "/hist");
                string formedUrl = (histParameters is null ? 
                    baseUrl : QueryHelpers.AddQueryString(baseUrl, histParameters));

                if (!Uri.IsWellFormedUriString(formedUrl, UriKind.Absolute))
                    return null;

                HttpRequestMessage request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(formedUrl),
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
            });
        }
        public Task<string?> GetCandlesAsync(string candle, Dictionary<string, string?> histParameters)
        {
            return GetInfoAsync(candle, histParameters, "candles");
        }
        public Task<string?> GetTickersAsync(string symbol)
        {
            return GetInfoAsync(symbol, null, "ticker");
        }

        public Task<string?> GetTradesAsync(string symbol, Dictionary<string, string?> histParameters)
        {
            return GetInfoAsync(symbol, histParameters, "trades");
        }
    }
}
