using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebSocketSharp.Net.WebSockets
{
    public class ServerSession
    {
        public Guid Id { get; private set; }
        public ServerWebSocket WebSocket { get; private set; }
        public object Data { get; set; }

        public ServerSession(ServerWebSocket socket)
        {
            WebSocket = socket;
            //Todo: add last activity time
            Id = Guid.NewGuid();
        }
    }
}
