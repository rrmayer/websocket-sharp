using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace WebSocketSharp.Net.WebSockets
{
    public sealed class ClientWebSocket : WebSocket
    {
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
        public ClientWebSocket(string url, params string[] protocols)
        {
            if (url.IsNull())
                throw new ArgumentNullException("url");

            Uri uri;
            string msg;
            if (!url.TryCreateWebSocketUri(out uri, out msg))
                throw new ArgumentException(msg, "url");

            _uri = uri;
            _protocols = protocols.ToString(", ");
            _client = true;
            _secure = uri.Scheme == "wss"
                          ? true
                          : false;
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
        private RequestHandshake createRequestHandshake()
        {
            var path = _uri.PathAndQuery;
            var host = _uri.Port == 80
                     ? _uri.DnsSafeHost
                     : _uri.Authority;

            var req = new RequestHandshake(path);
            req.AddHeader("Host", host);

            if (!_origin.IsEmpty())
                req.AddHeader("Origin", _origin);

            req.AddHeader("Sec-WebSocket-Key", _base64key);

            if (!_protocols.IsNullOrEmpty())
                req.AddHeader("Sec-WebSocket-Protocol", _protocols);

            var extensions = createRequestExtensions();
            if (!extensions.IsEmpty())
                req.AddHeader("Sec-WebSocket-Extensions", extensions);

            req.AddHeader("Sec-WebSocket-Version", _version);

            if (_cookies.Count > 0)
                req.SetCookies(_cookies);

            return req;
        }

        // As client
        private bool doHandshake()
        {
            init();
            sendRequestHandshake();
            return processResponseHandshake();
        }

        // As client
        private void init()
        {
            _base64key = createBase64Key();

            var host = _uri.DnsSafeHost;
            var port = _uri.Port;
            _tcpClient = new TcpClient(host, port);
            _wsStream = WsStream.CreateClientStream(_tcpClient, host, _secure);
        }

        // As client
        private bool isValidResponseHandshake(ResponseHandshake response)
        {
            return !response.IsWebSocketResponse
                   ? false
                   : !response.HeaderExists("Sec-WebSocket-Accept", createResponseKey())
                     ? false
                     : !response.HeaderExists("Sec-WebSocket-Version") ||
                       response.HeaderExists("Sec-WebSocket-Version", _version);
        }

        // As client
        private void processResponseCookies(CookieCollection cookies)
        {
            if (cookies.Count > 0)
                _cookies.SetOrRemove(cookies);
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
                _protocol = protocol;
        }

        // As client
        private void sendRequestHandshake()
        {
            var req = createRequestHandshake();
            send(req);
        }


        // As client
        private void closeResourcesAsClient()
        {
            if (!_wsStream.IsNull())
            {
                _wsStream.Dispose();
                _wsStream = null;
            }

            if (!_tcpClient.IsNull())
            {
                _tcpClient.Close();
                _tcpClient = null;
            }
        }
    }
}
