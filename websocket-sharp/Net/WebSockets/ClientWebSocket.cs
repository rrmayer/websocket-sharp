using System;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace WebSocketSharp.Net.WebSockets
{
    public sealed class ClientWebSocket : WebSocket
    {
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
            if (IsOpened(true))
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
            UnderlyingStream = WebSocketStream.CreateClientStream(client, host, IsSecure);

            try
            {
                if (doHandshake(uri))
                    onOpen();
            }
            catch (Exception ex)
            {
                var msg = "An exception has occured: " + ex.GetType().Name + ". " + ex.Message;
                onError(msg);
                Close(CloseStatusCode.ABNORMAL, msg);
            }
        }

        // As client
        private string createRequestExtensions()
        {
            var extensions = new StringBuilder(64);
            var compress = CreateCompressExtension(Compression);
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

            var req = new RequestHandshake(path.ToUri());
            req.AddHeader(HeaderConstants.HOST, host);

            if (!Origin.IsEmpty())
                req.AddHeader(HeaderConstants.ORIGIN, Origin);

            if (!SubProtocol.IsNullOrEmpty())
                req.AddHeader(HeaderConstants.SEC_WEBSOCKET_PROTOCOL, SubProtocol);

            var extensions = createRequestExtensions();
            if (!extensions.IsEmpty())
                req.AddHeader(HeaderConstants.SEC_WEBSOCKET_EXTENSIONS, extensions);

            if (Options.Cookies.Count > 0)
                req.SetCookies(Options.Cookies);

            return req;
        }

        // As client
        private bool doHandshake(Uri requestUri)
        {
            var request = createRequestHandshake(requestUri);
            send(request);

            var response = receiveResponseHandshakeFromStream();  //todo: add timeout? currently 30 seconds.
            return processResponseHandshake(request, response);
        }

        // As client
        private ResponseHandshake receiveResponseHandshakeFromStream()
        {
            var res = ResponseHandshake.ReadFromStream(WebSocketStream);
#if DEBUG
            Console.WriteLine("WS: Info@receiveResponseHandshake: Response handshake from server:\n");
            Console.WriteLine(res.ToString());
#endif
            return res;
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
                    if (!compress && isCompressExtension(e, Compression))
                        compress = true;
                }

                Extensions = extensions;
            }

            if (!compress)
                Compression = CompressionMethod.NONE;
        }

        // As client
        private bool processResponseHandshake(RequestHandshake handshake, ResponseHandshake response)
        {
            if (!response.IsCorrespondingResponse(handshake))
            {
                var msg = "Invalid response to this WebSocket connection request.";
                onError(msg);
                Close(CloseStatusCode.ABNORMAL, msg);

                return false;
            }

            SubProtocol = response.SubProtocol;
            processResponseExtensions(response.Extensions);
            processResponseCookies(response.Cookies);

            return true;
        }

        // As client
        protected override void OnClosing()
        {
            if (!UnderlyingStream.IsNull())
            {
                UnderlyingStream.Dispose();
                UnderlyingStream = null;
            }
        }

        protected override bool IsClientWebSocket
        {
            get { return true; }
        }
    }
}
