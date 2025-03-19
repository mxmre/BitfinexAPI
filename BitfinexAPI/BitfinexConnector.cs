using BitfinexAPI.Interfaces;
using BitfinexAPI.TestHQ;
using BitfinexAPI.TestHQ.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BitfinexAPI
{
    public class BitfinexConnector : ITestConnector
    {
        #region ConnectorRealization
        private IClientRestAPI _clientRestAPI;
        private IClientWebsocketAPI _clientWebsocketAPI;
        private Task task;

        private static Dictionary<string, int> _subscribedCandles;
        private static Dictionary<string, int> _subscribedTrades;

        public void WAIT()
        {
            task.Wait();
        }
        static BitfinexConnector()
        {
            _subscribedCandles = new();
            _subscribedTrades = new();
        }

        public BitfinexConnector(
            IClientRestAPI clientRestAPI,
            IClientWebsocketAPI clientWebsocketAPI)
        {
            _clientRestAPI = clientRestAPI;
            _clientWebsocketAPI = clientWebsocketAPI;
            _clientWebsocketAPI.OnMessageReceived += OnMessageReceived;

            
        }
        public void Reconnect()
        {
            if (!_clientWebsocketAPI.ConnectionIsOpen())
            {
                _clientWebsocketAPI.Connect().Wait();
                task = _clientWebsocketAPI.GetMessageAsync();
            }
        }
        #endregion

        #region Rest
        public Task<IEnumerable<Candle>> GetCandleSeriesAsync(string pair, int periodInSec, DateTimeOffset? from, DateTimeOffset? to = null, long? count = 0)
        {
            return Task<IEnumerable<Candle>>.Factory.StartNew(() =>
            {
                Dictionary<string, string?>? parameters = new Dictionary<string, string?>();
                bool parametersIsNull = true;
                {
                    if(from is not null)
                    {
                        parametersIsNull = false;
                        parameters.Add("start", from.Value.ToUnixTimeMilliseconds().ToString());
                    }
                    if (to is not null)
                    {
                        parametersIsNull = false;
                        parameters.Add("end", to.Value.ToUnixTimeMilliseconds().ToString());
                    }
                    if (count is not null && count >= 0 && count <= 10000)
                    {
                        parametersIsNull = false;
                        parameters.Add("limit", count.ToString());
                    }
                    if(parametersIsNull)
                    {
                        parameters = null;
                    }
                }
                string candleName = "trade:1m:" + pair + ":p" + periodInSec.ToString();
                var jsonBody = _clientRestAPI.GetCandlesAsync(candleName, "hist", parameters).Result;

                var strList = JsonSerializer.Deserialize<List<List<double>>>(jsonBody);
                var candleList = new List<Candle>();
                foreach (var innerList in strList)
                {
                    var iterator = innerList.GetEnumerator();

                    //парсим свечу
                    Candle candle = new Candle();
                    candle.Pair = pair;
                    candle.OpenTime = DateTimeOffset.FromUnixTimeMilliseconds
                        ((long)iterator.Current); iterator.MoveNext();
                    candle.OpenPrice = (decimal)iterator.Current; iterator.MoveNext();
                    candle.ClosePrice = (decimal)(iterator.Current); iterator.MoveNext();
                    candle.HighPrice = (decimal)(iterator.Current); iterator.MoveNext();
                    candle.LowPrice = (decimal)(iterator.Current); iterator.MoveNext();
                    candle.TotalVolume = (decimal)(iterator.Current);
                    candle.TotalPrice = candle.OpenPrice - candle.ClosePrice;

                    candleList.Add(candle);
                }
                return candleList;
            }, TaskCreationOptions.AttachedToParent);
        }

        public Task<IEnumerable<Trade>> GetNewTradesAsync(string pair, int maxCount)
        {
            return Task<IEnumerable<Trade>>.Factory.StartNew(() =>
            {
                var jsonBody = _clientRestAPI.GetTradesAsync("t" + pair, new Dictionary<string, string?> 
                { 
                    ["limit"] = maxCount.ToString()
                }).Result;
                var strList = JsonSerializer.Deserialize<List<List<string>>>(jsonBody);
                var tradeList = new List<Trade>();
                foreach(var innerList in strList)
                {
                    var iterator = innerList.GetEnumerator();

                    Trade trade = new Trade();
                    trade.Pair = "t" + pair;
                    trade.Id = iterator.Current; iterator.MoveNext();
                    trade.Time = DateTimeOffset.FromUnixTimeMilliseconds
                        (int.Parse(iterator.Current)); iterator.MoveNext();
                    trade.Amount = decimal.Parse(iterator.Current); iterator.MoveNext();
                    trade.Side = trade.Amount < 0.0M ? "sell" : "buy";
                    trade.Price = decimal.Parse(iterator.Current);
                    tradeList.Add(trade);
                }
                return tradeList;
            }, TaskCreationOptions.AttachedToParent);
        }
        #endregion

        #region Socket
        public event Action<Trade>? NewBuyTrade;
        public event Action<Trade>? NewSellTrade;
        public event Action<Candle>? CandleSeriesProcessing;
        
        static private void OnMessageReceived(IClientWebsocketAPI sender, string msg)
        {
            if(msg.Contains("\"event\""))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(msg);
                if (msg.Contains("\"unsubscribed\""))
                {
                    if (dict is not null && dict.ContainsKey("status") && dict["status"] == "OK")
                    {
                        int chanId = int.Parse(dict["chanId"]);
                        if (_subscribedCandles.ContainsValue(chanId))
                        {
                            _subscribedCandles.Remove(_subscribedCandles.FirstOrDefault(x => x.Value == chanId).Key);
                        }
                        else if (_subscribedTrades.ContainsValue(chanId))
                        {
                            _subscribedTrades.Remove(_subscribedTrades.FirstOrDefault(x => x.Value == chanId).Key);
                        }

                    }
                }
                else if (msg.Contains("\"subscribed\""))
                {

                }
            }
        }
        public void SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = 0)
        {
            Reconnect();
            _clientWebsocketAPI.SendMessageAsync(JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["event"] = "subscribe",
                ["channel"] = "candles",
                ["key"] = "trade:1m:" + pair + (periodInSec == 0 ? "" : ":p" + periodInSec.ToString()),
                ["from"] = from?.ToUnixTimeMilliseconds().ToString(),
                ["to"] = to?.ToUnixTimeMilliseconds().ToString(),
                ["limit"] = count.ToString(),
            })).Wait();

        }

        public void SubscribeTrades(string pair, int maxCount = 100)
        {
            Reconnect();
            _clientWebsocketAPI.SendMessageAsync(JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["event"] = "subscribe",
                ["channel"] = "trades",
                ["symbol"] = pair,
                ["len"] = maxCount.ToString(),
            })).Wait();

        }

        public void UnsubscribeCandles(string pair)
        {
            if (!_subscribedCandles.ContainsKey(pair))
                return;
            _clientWebsocketAPI.SendMessageAsync(JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["event"] = "unsubscribe",
                ["chanId"] = _subscribedCandles[pair].ToString(),
            })).Wait();
        }

        public void UnsubscribeTrades(string pair)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
