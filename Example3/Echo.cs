using System;
using WebSocketSharp;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace Example3 {

  public class Echo : IWebSocketService
  {
      public void OnOpen(ServerSession session)
      {

      }

      public void OnReceive(ServerSession session, MessageEventArgs e)
      {
          var msg = session.WebSocket.Context.QueryString.Exists("name")
                  ? String.Format("'{0}' returns to {1}", e.Data, session.WebSocket.Context.QueryString["name"])
                  : e.Data;
          session.WebSocket.Send(msg);
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
