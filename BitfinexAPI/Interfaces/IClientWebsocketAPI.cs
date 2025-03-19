using BitfinexAPI.TestHQ;

namespace BitfinexAPI.Interfaces
{
    public interface IClientWebsocketAPI
    {
        public delegate void OnMessageReceivedEventHandler(IClientWebsocketAPI sender, string msg);
        event OnMessageReceivedEventHandler OnMessageReceived;
        Task GetMessageAsync();
        Task SendMessageAsync(string msg);
        public Task Connect();
        public Task Disconnect();
        public bool ConnectionIsOpen();
    }
}
