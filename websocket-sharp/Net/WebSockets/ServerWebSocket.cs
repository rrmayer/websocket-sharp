using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace WebSocketSharp.Net.WebSockets
{
    public sealed class ServerWebSocket : WebSocket
    {
        public WebSocketContext Context {get; private set;}

        public ServerWebSocket(WebSocketContext context)
        {
            Context = context;
            init();
        }

        // As server
        private ResponseHandshake createResponseHandshake()
        {
            var res = new ResponseHandshake();
            res.AddHeader("Sec-WebSocket-Accept", createResponseKey());
            if (!_extensions.IsEmpty())
                res.AddHeader("Sec-WebSocket-Extensions", _extensions);

            if (Context.Cookies.Any())
                res.SetCookies(new CookieCollection(Context.Cookies));

            return res;
        }

        // As server
        private ResponseHandshake createResponseHandshake(HttpStatusCode code)
        {
            var res = ResponseHandshake.CreateCloseResponse(code);
            res.AddHeader("Sec-WebSocket-Version", _version);

            return res;
        }

        // As server
        private void init()
        {
            _wsStream = Context.Stream;
            _closeContext = Context.Close;
            _uri = context.Path.ToUri();
            _secure = context.IsSecureConnection;
            _client = false;
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
            var authority = _context.Headers["Host"];
            if (authority.IsNullOrEmpty() || !_uri.IsAbsoluteUri)
                return true;

            var i = authority.IndexOf(':');
            var host = i > 0
                     ? authority.Substring(0, i)
                     : authority;
            var type = Uri.CheckHostName(host);

            return type != UriHostNameType.Dns
                   ? true
                   : Uri.CheckHostName(_uri.DnsSafeHost) != UriHostNameType.Dns
                     ? true
                     : host == _uri.DnsSafeHost;
        }

        // As server
        private bool isValidRequesHandshake1544955205()
        {
            return !_context.IsValid
                   ? false
                   : !isValidHostHeader()
                     ? false
                     : _context.Headers.Exists("Sec-WebSocket-Version", _version);
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
                        _compression = CompressionMethod.DEFLATE;
                        compress = true;
                    }

                    if (!compress && isCompressExtension(tmp, CompressionMethod.DEFLATE))
                    {
                        _compression = CompressionMethod.DEFLATE;
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
                _extensions = buffer.ToArray().ToString(",");
        }

        // As server
        private bool processRequestHandshake()
        {
#if DEBUG
            var req = RequestHandshake.Parse(_context);
            Console.WriteLine("WS: Info@processRequestHandshake: Request handshake from client:\n");
            Console.WriteLine(req.ToString());
#endif
            if (!isValidRequesHandshake())
            {
                onError("Invalid WebSocket connection request.");
                close(HttpStatusCode.BadRequest);
                return false;
            }

            _base64key = _context.SecWebSocketKey;
            processRequestProtocols(_context.Headers["Sec-WebSocket-Protocol"]);
            processRequestExtensions(_context.Headers["Sec-WebSocket-Extensions"]);

            return true;
        }

        // As server
        private void processRequestProtocols(string protocols)
        {
            if (!protocols.IsNullOrEmpty())
                _protocols = protocols;
        }

        // As server
        private void sendResponseHandshake()
        {
            var res = createResponseHandshake();
            send(res);
        }

        // As server
        private void sendResponseHandshake(HttpStatusCode code)
        {
            var res = createResponseHandshake(code);
            send(res);
        }

        // As Server
        public void Close(HttpStatusCode code)
        {
            close(code);
        }

        private void close(HttpStatusCode code)
        {
            if (State != WebSocketState.CONNECTING || _client)
                return;

            sendResponseHandshake(code);
            closeResources();
        }

        // As server
        private bool acceptHandshake()
        {
            if (!processRequestHandshake())
                return false;

            sendResponseHandshake();
            return true;
        }

        private void close(PayloadData data)
        {
#if DEBUG
            Console.WriteLine("WS: Info@close: Current thread IsBackground?: {0}", Thread.CurrentThread.IsBackground);
#endif
            lock (_forClose)
            {
                // Whether the closing handshake has been started already?
                if (State == WebSocketState.CLOSING ||
                    State == WebSocketState.CLOSED)
                    return;

                // Whether the closing handshake on server is started before the connection has been established?
                if (State == WebSocketState.CONNECTING && !_client)
                {
                    sendResponseHandshake(HttpStatusCode.BadRequest);
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

            closeHandshake(data);
#if DEBUG
            Console.WriteLine("WS: Info@close: Exit close method.");
#endif
        }

        private void close(HttpStatusCode code)
        {
            if (State != WebSocketState.CONNECTING || _client)
                return;

            sendResponseHandshake(code);
            closeResources();
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

        private void closeHandshake(PayloadData data)
        {
            var args = new CloseEventArgs(data);
            var frame = createControlFrame(Opcode.CLOSE, data, _client);
            if (send(frame))
                args.WasClean = true;

            onClose(args);
        }

        protected override void OnClosing()
        {
            if (!_context.IsNull() && !_closeContext.IsNull())
            {
                _closeContext();
                _wsStream = null;
                _context = null;
            }
        }

        // As server
        private void closeResourcesAsServer()
        {

        }
    }
}
