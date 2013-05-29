using System;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
    public class ServerSession : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public Guid Id { get; private set; }
        public ServerWebSocket WebSocket { get; private set; }
        public object Data { get; set; }

        public ServerSession(ServerWebSocket socket)
        {
            WebSocket = socket;
            //Todo: add last activity time
            Id = Guid.NewGuid();
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            try { OnDisposing(); }
            catch { }
            IsDisposed = true;
        }

        protected virtual void OnDisposing()
        { }

        ~ServerSession()
        {
            Dispose();
        }
    }
}
