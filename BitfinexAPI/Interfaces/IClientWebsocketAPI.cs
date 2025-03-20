using BitfinexAPI.TestHQ;

namespace BitfinexAPI.Interfaces
{
    public interface IClientWebsocketAPI : IDisposable
    {
        public delegate void OnMessageReceivedEventHandler(IClientWebsocketAPI sender, string msg);
        event OnMessageReceivedEventHandler OnMessageReceived;
        Task GetMessageAsync(int msgSize);
        Task SendMessageAsync(string msg);
        public Task Connect();
        public Task Disconnect();
        public bool ConnectionIsOpen();
    }
}
