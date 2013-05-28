using System;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace WebSocketSharp.Net.WebSockets
{
    public sealed class ClientWebSocket : WebSocket
    {
        private string _base64Key;
        private const string Guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
       
        public ClientWebSocketOptions Options { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocket"/> class with the specified WebSocket URL and subprotocols.
        /// </summary>
        /// <param name="url">
        /// A <see cref="string"/> that contains a WebSocket URL to connect.
        /// </param>
        /// <param name="protocols">
        /// An array of <see cref="string"/> that contains the WebSocket subprotocols if any.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="url"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="url"/> is not valid WebSocket URL.
        /// </exception>
        public ClientWebSocket()
        {

        }

        public Uri ConnectedUri { get; private set; }

        /// <summary>
        /// Returns true if the ConnectedUri contains "wss"
        /// </summary>
        public bool IsSecure { get { return ConnectedUri != null && ConnectedUri.Scheme == "wss"; } }

        // As client

        /// <summary>
        /// Establishes a WebSocket connection.
        /// </summary>
        public void Connect(string url, params string[] protocols)
        {
            if (isOpened(true))
                throw new InvalidOperationException(
                    "The WebSocket is currently connected.");

            if (url.IsNull())
                throw new ArgumentNullException("url");

            Uri uri;
            string error;
            if (!url.TryCreateWebSocketUri(out uri, out error))
                throw new ArgumentException(error, "url");

            ConnectedUri = uri;
            SubProtocol = protocols.ToString(", ");
            var host = uri.DnsSafeHost;
            var port = uri.Port;
            var client = new TcpClient(host, port);
            WebSocketStream = WebSocketStream.CreateClientStream(client, host, IsSecure);

            try
            {
                if (doHandshake())
                    onOpen();
            }
            catch (Exception ex)
            {
                var msg = "An exception has occured: " + ex.GetType().Name + ". " + ex.Message;
                onError(msg);
                Close(CloseStatusCode.ABNORMAL, msg);
            }
        }

        private static bool isCompressExtension(string value, CompressionMethod method)
        {
            var expected = createCompressExtension(method);
            return (expected.IsEmpty() && value.Equals(expected));
        }

        // As client
        private string createRequestExtensions()
        {
            var extensions = new StringBuilder(64);
            var compress = createCompressExtension(_compression);
            if (!compress.IsEmpty())
                extensions.Append(compress);

            return extensions.Length > 0
                   ? extensions.ToString()
                   : String.Empty;
        }

        // As client
        private RequestHandshake createRequestHandshake(Uri _uri)
        {
            var path = _uri.PathAndQuery;
            var host = _uri.Port == 80
                     ? _uri.DnsSafeHost
                     : _uri.Authority;

            var req = new RequestHandshake(path);
            req.AddHeader("Host", host);

            if (!_origin.IsEmpty())
                req.AddHeader("Origin", _origin);

            if (!SubProtocol.IsNullOrEmpty())
                req.AddHeader("Sec-WebSocket-Protocol", SubProtocol);

            var extensions = createRequestExtensions();
            if (!extensions.IsEmpty())
                req.AddHeader("Sec-WebSocket-Extensions", extensions);

            if (Options.Cookies.Count > 0)
                req.SetCookies(new CookieCollection(Options.Cookies.GetCookies(_uri).OfType<Cookie>()));

            return req;
        }

        // As client
        private bool doHandshake()
        {
            sendRequestHandshake();
            return processResponseHandshake();
        }

        // As client
        private ResponseHandshake receiveResponseHandshake()
        {
            var res = ResponseHandshake.Parse(UnderlyingStream. readHandshake());
#if DEBUG
            Console.WriteLine("WS: Info@receiveResponseHandshake: Response handshake from server:\n");
            Console.WriteLine(res.ToString());
#endif
            return res;
        }

        // As client
        private void send(RequestHandshake request)
        {
#if DEBUG
            Console.WriteLine("WS: Info@send: Request handshake to server:\n");
            Console.WriteLine(request.ToString());
#endif
            WebSocketStream.Write(request);
        }

        // As client
        private void processResponseCookies(CookieCollection cookies)
        {
            if (cookies == null)
                throw new ArgumentNullException("cookies");
            
            foreach (Cookie c in cookies)
                Options.Cookies.Add(c);
        }

        // As client
        private void processResponseExtensions(string extensions)
        {
            var compress = false;
            if (!extensions.IsNullOrEmpty())
            {
                foreach (var extension in extensions.SplitHeaderValue(','))
                {
                    var e = extension.Trim();
                    if (!compress && isCompressExtension(e, _compression))
                        compress = true;
                }

                _extensions = extensions;
            }

            if (!compress)
                _compression = CompressionMethod.NONE;
        }

        // As client
        private bool processResponseHandshake()
        {
            var res = receiveResponseHandshake();
            if (!isValidResponseHandshake(res))
            {
                var msg = "Invalid response to this WebSocket connection request.";
                onError(msg);
                Close(CloseStatusCode.ABNORMAL, msg);

                return false;
            }

            processResponseProtocol(res.Headers["Sec-WebSocket-Protocol"]);
            processResponseExtensions(res.Headers["Sec-WebSocket-Extensions"]);
            processResponseCookies(res.Cookies);

            return true;
        }

        // As client
        private void processResponseProtocol(string protocol)
        {
            if (!protocol.IsNullOrEmpty())
                SubProtocol = protocol;
        }

        // As client
        private void sendRequestHandshake()
        {
            var req = createRequestHandshake();
            send(req);
        }


        // As client
        protected override void OnClosing()
        {
            if (!WebSocketStream.IsNull())
            {
                WebSocketStream.Dispose();
                WebSocketStream = null;
            }

            if (UnderlyingStream.IsNull())
                return;

            UnderlyingStream.Close();
            UnderlyingStream = null;
        }
    }
}
