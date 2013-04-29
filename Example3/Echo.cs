using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example3 {

  public class Echo : WebSocketSession
  {
    protected override void OnMessage(MessageEventArgs e)
    {
      var msg = QueryString.Exists("name")
              ? String.Format("'{0}' returns to {1}", e.Data, QueryString["name"])
              : e.Data;
      Send(msg);
    }
  }
}
