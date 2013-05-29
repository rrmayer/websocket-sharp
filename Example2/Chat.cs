using System;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace Example2
{

    public class ChatSession : ServerSession
    {
        public string Name { get; set; }

        public ChatSession(ServerWebSocket socket)
            : base(socket)
        {
        }
    }

    public class Chat : IWebSocketService
    {
        private static int _num = 0;

        private static string getName(WebSocketContext context)
        {
            return context.QueryString.Exists("name")
                   ? context.QueryString["name"]
                   : "anon#" + getNum();
        }

        private static int getNum()
        {
            return Interlocked.Increment(ref _num);
        }

        public void OnOpen(ServerSession session)
        {
            throw new NotImplementedException();
        }

        public void OnReceive(ServerSession session, MessageEventArgs e)
        {
            var msg = String.Format("{0}: {1}", ((ChatSession)session).Name, e.Data);
            ServiceHost.Broadcast(msg);
        }

        public void OnClose(ServerSession session, CloseEventArgs e)
        {
            var msg = String.Format("{0} got logged off...", ((ChatSession)session).Name);
            ServiceHost.Broadcast(msg);
        }

        public void OnError(ServerSession session, ErrorEventArgs e)
        {
            //Do nothing.
        }

        public IWebSocketServiceHost ServiceHost { set; private get; }

        public Func<ServerWebSocket, ServerSession> SessionFactory
        {
            get { return (a) => new ChatSession(a) {Name = getName(a.Context)}; }
        }
    }
}
