using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WebSocketSharp.Net.WebSockets
{
    public sealed class ServerWebSocket : WebSocket
    {
        public WebSocketContext Context { get; private set; }

        public ServerWebSocket(TcpClient client, bool useTls)
        {
            var stream = WebSocketStream.CreateServerStream(client, useTls);
            var request = RequestHandshake.Parse(stream.ReadHandshake(TimeSpan.FromSeconds(30)));
            Context = new TcpListenerWebSocketContext(request, client, useTls);
            UnderlyingStream = stream;
        }

        public ServerWebSocket(HttpListenerContext context)
        {
            Context = new HttpListenerWebSocketContext(context);
            UnderlyingStream = WebSocketStream.CreateServerStream(context);
        }

        // As server
        private ResponseHandshake generateResponseHandshake(RequestHandshake handshake)
        {
            var res = new ResponseHandshake(handshake.Base64Key);
            if (!Extensions.IsEmpty())
                res.AddHeader(HeaderConstants.SEC_WEBSOCKET_EXTENSIONS, Extensions);

            if (Context.Cookies.Any())
                res.SetCookies(new CookieCollection(Context.Cookies));

            return res;
        }

        // As server
        private ResponseHandshake createResponseHandshake(HttpStatusCode code)
        {
            var res = ResponseHandshake.CreateCloseResponse(code);

            return res;
        }

        // As server

        private static bool isPerFrameCompressExtension(string value)
        {
            if (value.Equals("deflate-frame"))
                return true;

            if (value.Equals("perframe-compress; method=deflate"))
                return true;

            return false;
        }

        // As server
        private bool isValidHostHeader1808838690()
        {
            var authority = Context.Headers["Host"];
            if (authority.IsNullOrEmpty() || !Context.RequestUri.IsAbsoluteUri)
                return true;

            var i = authority.IndexOf(':');
            var host = i > 0
                     ? authority.Substring(0, i)
                     : authority;
            var type = Uri.CheckHostName(host);

            var rtn = type != UriHostNameType.Dns || (Uri.CheckHostName(Context.RequestUri.DnsSafeHost) != UriHostNameType.Dns || host == Context.RequestUri.DnsSafeHost);

            return rtn;
        }

        // As server
        private void processRequestExtensions(string extensions)
        {
            if (extensions.IsNullOrEmpty())
                return;

            var compress = false;
            var buffer = new List<string>();
            foreach (var extension in extensions.SplitHeaderValue(','))
            {
                var e = extension.Trim();
                var tmp = e.StartsWith("x-webkit-")
                        ? e.Substring(9)
                        : e;

                if (!compress)
                {
                    if (ServerWebSocket.isPerFrameCompressExtension(tmp))
                    {
                        _perFrameCompress = true;
                        Compression = CompressionMethod.DEFLATE;
                        compress = true;
                    }

                    if (!compress && isCompressExtension(tmp, CompressionMethod.DEFLATE))
                    {
                        Compression = CompressionMethod.DEFLATE;
                        compress = true;
                    }

                    if (compress)
                    {
                        buffer.Add(e);
                        continue;
                    }
                }
            }

            if (buffer.Count > 0)
                Extensions = buffer.ToArray().ToString(",");
        }

        // As server
        private bool processRequestHandshake(out RequestHandshake handshake)
        {
            handshake = RequestHandshake.FromContext(Context);
#if DEBUG
            Console.WriteLine("WS: Info@processRequestHandshake: Request handshake from client:\n");
            Console.WriteLine(handshake.ToString());
#endif
            if (!handshake.IsValid)
            {
                onError("Invalid WebSocket connection request.");
                close(HttpStatusCode.BadRequest);
                return false;
            }

            return true;
        }

        // As server
        private void sendResponseHandshake(RequestHandshake handshake)
        {
            var res = generateResponseHandshake(handshake);
            send(res);
        }

        // As Server
        public void Close(HttpStatusCode code)
        {
            close(code);
        }

        private void close(HttpStatusCode code)
        {
            if (State != WebSocketState.CONNECTING)
                return;

            sendClosingResponseHandshake(code);
            closeResources();
        }

        // As server
        private bool acceptHandshake()
        {
            RequestHandshake handshake;
            if (!processRequestHandshake(out handshake))
                return false;

            sendResponseHandshake(handshake);
            return true;
        }

        private void close(PayloadData data)
        {
#if DEBUG
            Console.WriteLine("WS: Info@close: Current thread IsBackground?: {0}", Thread.CurrentThread.IsBackground);
#endif
            lock (_closeLock)
            {
                // Whether the closing handshake has been started already?
                if (State == WebSocketState.CLOSING ||
                    State == WebSocketState.CLOSED)
                    return;

                // Whether the closing handshake on server is started before the connection has been established?
                if (State == WebSocketState.CONNECTING)
                {
                    sendClosingResponseHandshake(HttpStatusCode.BadRequest);
                    onClose(new CloseEventArgs(data));

                    return;
                }

                State = WebSocketState.CLOSING;
            }

            // Whether a payload data contains the close status code which must not be set for send?
            if (data.ContainsReservedCloseStatusCode)
            {
                onClose(new CloseEventArgs(data));
                return;
            }

            sendCloseHandshake(data);
#if DEBUG
            Console.WriteLine("WS: Info@close: Exit close method.");
#endif
        }

        protected override bool IsClientWebSocket
        {
            get { return false; }
        }

        private void close(ushort code, string reason)
        {
            using (var buffer = new MemoryStream())
            {
                var tmp = code.ToByteArray(ByteOrder.BIG);
                buffer.Write(tmp, 0, tmp.Length);
                if (!reason.IsNullOrEmpty())
                {
                    tmp = Encoding.UTF8.GetBytes(reason);
                    buffer.Write(tmp, 0, tmp.Length);
                }

                buffer.Close();
                var data = buffer.ToArray();
                if (data.Length > 125)
                {
                    var msg = "The payload length of a Close frame must be 125 bytes or less.";
                    onError(msg);

                    return;
                }

                close(new PayloadData(data));
            }
        }

        protected override void OnClosing()
        {

        }

        // As server
        private void closeResourcesAsServer()
        {

        }
    }
}
