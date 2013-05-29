using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebSocketSharp
{
    static class HeaderConstants
    {
        public const string SEC_WEBSOCKET_ACCEPT = "Sec-WebSocket-Accept";
        public const string SEC_WEBSOCKET_PROTOCOL = "Sec-WebSocket-Protocol";
        public const string SEC_WEBSOCKET_EXTENSIONS = "Sec-WebSocket-Extensions";
        public const string SEC_WEBSOCKET_VERSION = "Sec-WebSocket-Version";
        public const string SEC_WEBSOCKET_KEY = "Sec-WebSocket-Key";
        public const string HOST = "Host";
        public const string ORIGIN = "Origin";

    }
}
