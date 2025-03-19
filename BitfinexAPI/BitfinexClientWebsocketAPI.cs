using BitfinexAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static BitfinexAPI.Interfaces.IClientWebsocketAPI;

namespace BitfinexAPI
{
    public class BitfinexClientWebsocketAPI : IClientWebsocketAPI
    {
        protected static readonly string _baseUrl = "wss://api-pub.bitfinex.com/ws/2";
        protected ClientWebSocket _clientWebSocket;

        #region SocketInit

        public BitfinexClientWebsocketAPI()
        {
            _clientWebSocket = new ClientWebSocket();
            OnMessageReceived = null;
        }

        public event OnMessageReceivedEventHandler? OnMessageReceived;

        #endregion

        public Task Connect()
        {
            return Task.Factory.StartNew(() =>
            {
                if (_clientWebSocket.State == WebSocketState.Open || _clientWebSocket.State == WebSocketState.Connecting)
                    return;
                else _clientWebSocket.ConnectAsync(new Uri(_baseUrl), CancellationToken.None).Wait();
            }, TaskCreationOptions.AttachedToParent);
        }

        public bool ConnectionIsOpen()
        {
            return _clientWebSocket.State == WebSocketState.Open;
        }

        public Task Disconnect()
        {
            return Task.Factory.StartNew(() =>
            {
                if (_clientWebSocket.State == WebSocketState.Open)
                {
                    _clientWebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing connection",
                        CancellationToken.None
                    ).Wait();
                }

                _clientWebSocket.Dispose();
                _clientWebSocket = new ClientWebSocket();
            }, TaskCreationOptions.AttachedToParent);
            
        }

        public Task GetMessageAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                var buffer = new byte[1024 * 4];
                while (ConnectionIsOpen())
                {
                    var result = _clientWebSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    ).Result;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        OnMessageReceived?.Invoke(this, message);
                    }
                }
            }, TaskCreationOptions.AttachedToParent);
        }

        public Task SendMessageAsync(string msg)
        {
            return Task.Factory.StartNew(() =>
            {
                if (!ConnectionIsOpen())
                    return;
                var bytes = Encoding.UTF8.GetBytes(msg);
                _clientWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                ).Wait();
            }, TaskCreationOptions.AttachedToParent);
        }
    }
}
