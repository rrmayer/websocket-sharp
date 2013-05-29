using System;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
    public interface IWebSocketService
    {
        void OnOpen(ServerSession session);
        void OnReceive(ServerSession session, MessageEventArgs e);
        void OnClose(ServerSession session, CloseEventArgs e);
        void OnError(ServerSession session, ErrorEventArgs e);

        IWebSocketServiceHost ServiceHost { set; }
        Func<ServerWebSocket, ServerSession> SessionFactory { get; }
    }
}
