using System;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace Example2
{
    public class Echo : IWebSocketService
    {
        //protected override bool ProcessCookies(CookieCollection request, CookieCollection response)
        //{
        //    foreach (Cookie cookie in request)
        //    {
        //        cookie.Expired = true;
        //        response.Add(cookie);
        //    }

        //    return true;
        //}

        public void OnOpen(ServerSession session)
        {

        }

        public void OnReceive(ServerSession session, MessageEventArgs e)
        {
            var msg = session.WebSocket.Context.QueryString.Exists("name")
                    ? String.Format("'{0}' returns to {1}", e.Data, session.WebSocket.Context.QueryString["name"])
                    : e.Data;
            session.WebSocket.Send(msg);

            session.WebSocket.Ping();
        }

        public void OnClose(ServerSession session, CloseEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void OnError(ServerSession session, ErrorEventArgs e)
        {
            throw new NotImplementedException();
        }

        public IWebSocketServiceHost ServiceHost { set; private get; }

        public Func<ServerWebSocket, ServerSession> SessionFactory { get { return (a) => new ServerSession(a); } }
    }
}
