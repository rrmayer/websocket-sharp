using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
    public interface IWebSocketService
    {
        void OnOpen(WebSocketContext context);
        void OnMessage(WebSocketContext context, MessageEventArgs e);
        void OnClose(WebSocketContext context, CloseEventArgs e);
        void OnError(WebSocketContext context, ErrorEventArgs e);

        IWebSocketServiceHost ServiceHost { set; }
    }
}
