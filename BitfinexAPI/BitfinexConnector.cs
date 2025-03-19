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

        public BitfinexConnector(IClientRestAPI clientRestAPI, IClientWebsocketAPI clientWebsocketAPI)
        {
            _clientRestAPI = clientRestAPI;
            _clientWebsocketAPI = clientWebsocketAPI;
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
                    if(!parametersIsNull)
                    {
                        parameters = null;
                    }
                }
                string candleName = "trade:1m:" + pair + ":p" + periodInSec.ToString();
                var jsonBody = _clientRestAPI.GetCandlesAsync(candleName, "hist", parameters).Result;

                var strList = JsonSerializer.Deserialize<List<List<string>>>(jsonBody);
                var candleList = new List<Candle>();
                foreach (var innerList in strList)
                {
                    var iterator = innerList.GetEnumerator();

                    //парсим свечу
                    Candle candle = new Candle();
                    candle.Pair = pair;
                    candle.OpenTime = DateTimeOffset.FromUnixTimeMilliseconds
                        (int.Parse(iterator.Current)); iterator.MoveNext();
                    candle.OpenPrice = decimal.Parse(iterator.Current); iterator.MoveNext();
                    candle.ClosePrice = decimal.Parse(iterator.Current); iterator.MoveNext();
                    candle.HighPrice = decimal.Parse(iterator.Current); iterator.MoveNext();
                    candle.LowPrice = decimal.Parse(iterator.Current); iterator.MoveNext();
                    candle.TotalVolume = decimal.Parse(iterator.Current);
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
                var jsonBody = _clientRestAPI.GetTradesAsync(pair, new Dictionary<string, string?> 
                { 
                    ["limit"] = maxCount.ToString()
                }).Result;
                var strList = JsonSerializer.Deserialize<List<List<string>>>(jsonBody);
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
                return tradeList;
            }, TaskCreationOptions.AttachedToParent);
        }
        #endregion

        #region Socket
        public event Action<Trade> NewBuyTrade;
        public event Action<Trade> NewSellTrade;
        public event Action<Candle> CandleSeriesProcessing;

        public void SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = 0)
        {
            throw new NotImplementedException();
        }

        public void SubscribeTrades(string pair, int maxCount = 100)
        {
            throw new NotImplementedException();
        }

        public void UnsubscribeCandles(string pair)
        {
            throw new NotImplementedException();
        }

        public void UnsubscribeTrades(string pair)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
