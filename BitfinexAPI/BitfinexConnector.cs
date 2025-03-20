using BitfinexAPI.Interfaces;
using BitfinexAPI.TestHQ;
using BitfinexAPI.TestHQ.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BitfinexAPI
{
    public class BitfinexConnector : ITestConnector, IDisposable
    {
        #region ConnectorRealization
        private IClientRestAPI _clientRestAPI;
        private IClientWebsocketAPI _clientWebsocketAPI;
        private CancellationTokenSource globalCancellationToken;

        private static Dictionary<string, int> _subscribedCandles;
        private static Dictionary<string, int> _subscribedTrades;
        private static Dictionary<int, bool> _subscribtions;
        private long _reconnectAttempts;

        public delegate void ConnectorEventHandler(BitfinexConnector sender);

        public event ConnectorEventHandler? OnWebsocketConnected;
        public event ConnectorEventHandler? OnWebsocketConnection;
        public event ConnectorEventHandler? OnWebsocketConnectionError;

        public event ConnectorEventHandler? OnWebsocketSubscribeCandles;
        public event ConnectorEventHandler? OnWebsocketSubscribeCandlesError;

        public event ConnectorEventHandler? OnWebsocketSubscribeTrades;
        public event ConnectorEventHandler? OnWebsocketSubscribeTradesError;

        public event ConnectorEventHandler? OnWebsocketUnSubscribeCandles;
        public event ConnectorEventHandler? OnWebsocketUnSubscribeCandlesError;

        public event ConnectorEventHandler? OnWebsocketUnSubscribeTrades;
        public event ConnectorEventHandler? OnWebsocketUnSubscribeTradesError;

        public event ConnectorEventHandler? OnRestGettingTrades;
        public event ConnectorEventHandler? OnRestGetTrades;
        public event ConnectorEventHandler? OnRestGettingTradesError;

        public event ConnectorEventHandler? OnRestGettingCandleSeries;
        public event ConnectorEventHandler? OnRestGetCandleSeries;
        public event ConnectorEventHandler? OnRestGettingCandleSeriesError;

        private static string GetKeyFromValue(Dictionary<string, int> dict, int Value)
        {
            return dict.FirstOrDefault(x => x.Value == Value).Key;
        }
        static BitfinexConnector()
        {
            _subscribedCandles = new();
            _subscribedTrades = new();
            _subscribtions = new();
        }

        public BitfinexConnector(
            IClientRestAPI clientRestAPI,
            IClientWebsocketAPI clientWebsocketAPI)
        {
            _clientRestAPI = clientRestAPI;
            _clientWebsocketAPI = clientWebsocketAPI;
            _clientWebsocketAPI.OnMessageReceived += OnMessageReceived;
            _reconnectAttempts = 0;
            globalCancellationToken = new();
            Connect();
            ReconnectAttemptsCheck();

        }
        ~BitfinexConnector()
        {
            Dispose();
        }
        public void Dispose()
        {
            globalCancellationToken.Cancel();
            globalCancellationToken.Dispose();
            _clientRestAPI?.Dispose();
            _clientWebsocketAPI?.Dispose();
        }
        private Task ReconnectAttemptsCheck()
        {
            return Task.Factory.StartNew(() => 
            {
                DateTime start = DateTime.Now;
                int timeElapsedSec = 0;
                TimeSpan duration;
                while (true)
                {
                    Thread.Sleep(3000);
                    duration = DateTime.Now - start;
                    
                    
                    timeElapsedSec += (int)duration.TotalSeconds;
                    if (timeElapsedSec > 15)
                    {
                        timeElapsedSec -= 15;
                        if(Interlocked.Read(ref _reconnectAttempts) > 0)
                            Interlocked.Decrement(ref _reconnectAttempts);
                    }
                    start = DateTime.Now;
                }
            }, globalCancellationToken.Token);
        }
        
        #endregion

        #region Rest
        public Task<IEnumerable<Candle>> GetCandleSeriesAsync(string pair, int periodInSec, DateTimeOffset? from, DateTimeOffset? to = null, long? count = 0)
        {
            return Task<IEnumerable<Candle>>.Factory.StartNew(() =>
            {
                OnRestGettingCandleSeries?.Invoke(this);
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

                if (jsonBody is null)
                {
                    OnRestGettingCandleSeriesError?.Invoke(this);
                    return new List<Candle>();
                }

                var strList = JsonSerializer.Deserialize<List<List<double>>>(jsonBody);
                if (strList is null)
                {
                    OnRestGettingCandleSeriesError?.Invoke(this);
                    return new List<Candle>();
                }
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
                OnRestGetCandleSeries?.Invoke(this);
                return candleList;
            }, globalCancellationToken.Token);
        }

        public Task<IEnumerable<Trade>> GetNewTradesAsync(string pair, int maxCount)
        {
            return Task<IEnumerable<Trade>>.Factory.StartNew(() =>
            {
                OnRestGettingTrades?.Invoke(this);
                
                
                var jsonBody = _clientRestAPI.GetTradesAsync("t" + pair, new Dictionary<string, string?> 
                { 
                    ["limit"] = maxCount.ToString()
                }).Result;
                if (jsonBody is null)
                {
                    OnRestGettingTradesError?.Invoke(this);
                    return new List<Trade>();
                }
                    
                var strList = JsonSerializer.Deserialize<List<List<string>>>(jsonBody);
                
                if (strList is null)
                {
                    OnRestGettingTradesError?.Invoke(this);
                    return new List<Trade>();
                }
                var tradeList = new List<Trade>();
                foreach(var innerList in strList)
                {
                    var iterator = innerList.GetEnumerator();

                    Trade trade = new Trade();
                    trade.Pair = pair;
                    trade.Id = iterator.Current; iterator.MoveNext();
                    trade.Time = DateTimeOffset.FromUnixTimeMilliseconds
                        (int.Parse(iterator.Current)); iterator.MoveNext();
                    trade.Amount = decimal.Parse(iterator.Current); iterator.MoveNext();
                    trade.Side = trade.Amount < 0.0M ? "sell" : "buy";
                    trade.Price = decimal.Parse(iterator.Current);
                    tradeList.Add(trade);
                }
                OnRestGetTrades?.Invoke(this);
                return tradeList;
            }, globalCancellationToken.Token);
        }
        #endregion

        #region Socket
        public bool Connect()
        {
            OnWebsocketConnection?.Invoke(this);
            if (!_clientWebsocketAPI.ConnectionIsOpen() && Interlocked.Read(ref _reconnectAttempts) < 5)
            {
                _clientWebsocketAPI.Connect().Wait();
                _clientWebsocketAPI.GetMessageAsync(32 * 1024);
                Interlocked.Increment(ref _reconnectAttempts);
                OnWebsocketConnected?.Invoke(this);
                return true;
            }
            OnWebsocketConnectionError?.Invoke(this);
            return false;
        }
        public event Action<Trade>? NewBuyTrade;
        public event Action<Trade>? NewSellTrade;
        public event Action<Candle>? CandleSeriesProcessing;
        private enum MessageResponseType
        {
            Error,
            Info,
            Subscribed,
            Unsubscribed,
            Pong,
            Hb,
            Data
        }
        static private MessageResponseType GetMsgTypeFromStr(string msg)
        {
            if (msg.Contains("\"event\""))
            {
                if (msg.Contains("\"unsubscribed\""))
                {
                    return MessageResponseType.Unsubscribed;
                }
                else if (msg.Contains("\"subscribed\""))
                {
                    return MessageResponseType.Subscribed;
                }
                else if (msg.Contains("\"error\""))
                {
                    return MessageResponseType.Error;
                }
                else if (msg.Contains("\"info\""))
                {
                    return MessageResponseType.Info;
                }
                else if (msg.Contains("\"pong\""))
                {
                    return MessageResponseType.Pong;
                }
            }
            else if (msg.Contains("\"hb\""))
            {
                return MessageResponseType.Hb;
            }
            return MessageResponseType.Data;
        }
        private void OnMessageReceived(IClientWebsocketAPI sender, string msg)
        {
            MessageResponseType msgType = GetMsgTypeFromStr(msg);
            Dictionary<string, string>? dict = null;
            try
            {
                switch (msgType)
                {
                    case MessageResponseType.Error:
                    case MessageResponseType.Info:
                    case MessageResponseType.Pong:
                    case MessageResponseType.Hb:
                        return;
                    case MessageResponseType.Data:
                        {
                            using (JsonDocument doc = JsonDocument.Parse(msg))
                            {
                                Func<JsonElement, int, Trade> TradeFromJsonElement = (JsonElement elem, int chanId) => new Trade
                                {
                                    Id = elem[0].GetInt64().ToString(),
                                    Time = DateTimeOffset.FromUnixTimeMilliseconds(elem[1].GetInt64()),
                                    Amount = elem[2].GetDecimal(),
                                    Price = elem[3].GetDecimal(),
                                    Side = elem[2].GetDecimal() < 0.0M ? "sell" : "buy",
                                    Pair = GetKeyFromValue(_subscribedTrades, chanId)
                                };
                                Func<JsonElement, Candle> CandleFromJsonElement = (JsonElement elem) => new Candle
                                {
                                    OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(elem[0].GetInt64()),
                                    OpenPrice = elem[1].GetDecimal(),
                                    ClosePrice = elem[2].GetDecimal(),
                                    HighPrice = elem[3].GetDecimal(),
                                    LowPrice = elem[4].GetDecimal(),
                                    TotalVolume = elem[5].GetDecimal(),
                                    TotalPrice = elem[1].GetDecimal() - elem[2].GetDecimal(),
                                };
                                var root = doc.RootElement;

                                if (root.ValueKind == JsonValueKind.Array)
                                {
                                    var chanId = root[0].GetInt32();
                                    bool channelIsCandle = _subscribtions.ContainsKey(chanId);

                                    if (root[1][0].ValueKind == JsonValueKind.Array)
                                    {
                                        // Trade Snapshot
                                        if (!channelIsCandle)
                                        {
                                            var trades = root[1].EnumerateArray()
                                            .Select(trade => TradeFromJsonElement(trade, chanId)).ToList();
                                            foreach(var trade in trades)
                                            {
                                                if (trade.Side == "sell")
                                                {
                                                    NewSellTrade?.Invoke(trade);
                                                }
                                                else
                                                {
                                                    NewBuyTrade?.Invoke(trade);
                                                }
                                            }
                                        }
                                        // Candle Snapshot
                                        else
                                        {
                                            var candles = root[1].EnumerateArray()
                                           .Select(candle => CandleFromJsonElement(candle)).ToList();
                                            foreach (var candle in candles)
                                            {
                                                CandleSeriesProcessing?.Invoke(candle);
                                            }
                                        }
                                    }

                                    // Candle Update
                                    else if (root[1][0].ValueKind != JsonValueKind.Array && channelIsCandle)
                                    {
                                        Candle candle = CandleFromJsonElement(root[1]);
                                        CandleSeriesProcessing?.Invoke(candle);
                                    }
                                    // Trade Update
                                    if (root[1].ValueKind == JsonValueKind.String &&
                                        (root[1].GetString() == "te" || root[1].GetString() == "tu"))
                                    {
                                        Trade trade = TradeFromJsonElement(root[1], chanId);
                                        if(trade.Side == "sell")
                                        {
                                            NewSellTrade?.Invoke(trade);
                                        }
                                        else
                                        {
                                            NewBuyTrade?.Invoke(trade);
                                        }
                                    }



                                }
                            }
                            return;
                        }
                    case MessageResponseType.Unsubscribed:
                        {
                            dict = JsonSerializer.Deserialize<Dictionary<string, object>>(msg)?
                                .ToDictionary(
                                    kvp => kvp.Key,
                                    kvp => kvp.Value.ToString() ?? "error"
                                );
                            if (dict is null || !dict.ContainsKey("chanId"))
                                return;
                            int chanId = int.Parse(dict["chanId"]);
                            if (dict.ContainsKey("status") && dict["status"] == "OK")
                            {

                                if (_subscribtions.ContainsKey(chanId))
                                {
                                    if (_subscribtions[chanId])
                                    {
                                        _subscribedCandles.Remove(GetKeyFromValue(_subscribedCandles, chanId));
                                    }
                                    else
                                    {
                                        _subscribedTrades.Remove(GetKeyFromValue(_subscribedTrades, chanId));
                                    }
                                }
                            }
                            return;
                        }
                    case MessageResponseType.Subscribed:
                        {
                            dict = JsonSerializer.Deserialize<Dictionary<string, object>>(msg)?
                                .ToDictionary(
                                    kvp => kvp.Key,
                                    kvp => kvp.Value.ToString() ?? "error"
                                );
                            if (dict is null || !dict.ContainsKey("chanId"))
                                return;
                            int chanId = int.Parse(dict["chanId"]);
                            bool channel = dict["channel"] == "candles";
                            if (channel)
                            {
                                _subscribedCandles.Add(dict["key"].Split(":")[2], chanId);
                            }
                            else
                            {
                                _subscribedTrades.Add(dict["symbol"], chanId);
                            }
                            _subscribtions.Add(chanId, channel);
                            return;
                        }
                }
            }
            catch
            {
                return;
            }
            
        }
        public void SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = 0)
        {
            try
            {
                _clientWebsocketAPI.SendMessageAsync(JsonSerializer.Serialize(new Dictionary<string, string?>
                {
                    ["event"] = "subscribe",
                    ["channel"] = "candles",
                    ["key"] = "trade:1m:" + pair + (periodInSec == 0 ? "" : ":p" + periodInSec.ToString()),
                    ["from"] = from?.ToUnixTimeMilliseconds().ToString(),
                    ["to"] = to?.ToUnixTimeMilliseconds().ToString(),
                    ["limit"] = count.ToString(),
                })).Wait();
                OnWebsocketSubscribeCandles?.Invoke(this);
            }
            catch (Exception ex)
            {
                OnWebsocketSubscribeCandlesError?.Invoke(this);
                Connect();
            }
        }

        public void SubscribeTrades(string pair, int maxCount = 100)
        {
            
            try
            {
                _clientWebsocketAPI.SendMessageAsync(JsonSerializer.Serialize(new Dictionary<string, string?>
                {
                    ["event"] = "subscribe",
                    ["channel"] = "trades",
                    ["symbol"] = pair,
                    ["len"] = maxCount.ToString(),
                })).Wait();
                OnWebsocketSubscribeTrades?.Invoke(this);
            }
            catch (Exception ex)
            {
                Connect();
                OnWebsocketSubscribeTradesError?.Invoke(this);
            }
        }

        public void UnsubscribeCandles(string pair)
        {
            try
            {
                if (!_subscribedCandles.ContainsKey(pair))
                    return;
                _clientWebsocketAPI.SendMessageAsync(JsonSerializer.Serialize(new Dictionary<string, string?>
                {
                    ["event"] = "unsubscribe",
                    ["chanId"] = _subscribedCandles[pair].ToString(),
                })).Wait();
                OnWebsocketUnSubscribeCandles?.Invoke(this);
            }
            catch (Exception ex)
            {
                Connect();
                OnWebsocketUnSubscribeCandlesError?.Invoke(this);
            }
        }

        public void UnsubscribeTrades(string pair)
        {
            try
            {
                if (!_subscribedCandles.ContainsKey(pair))
                    return;
                _clientWebsocketAPI.SendMessageAsync(JsonSerializer.Serialize(new Dictionary<string, string?>
                {
                    ["event"] = "unsubscribe",
                    ["chanId"] = _subscribedTrades[pair].ToString(),
                })).Wait();
                OnWebsocketUnSubscribeTrades?.Invoke(this);
            }
            catch (Exception ex)
            {
                Connect();
                OnWebsocketUnSubscribeTradesError?.Invoke(this);
            }
        }


        #endregion

        
    }
}
