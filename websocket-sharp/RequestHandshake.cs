
/*
 * RequestHandshake.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2013 sta.blockhead
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */


using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp
{
    internal class RequestHandshake : Handshake
    {
        public const string Version = "13";
        private readonly string _base64Key;

        private NameValueCollection _queryString;

        public RequestHandshake(Uri uri)
        {
            HttpMethod = "GET";
            RequestUri = uri;
            AddHeader("User-Agent", "websocket-sharp/1.0");
            AddHeader("Upgrade", "websocket");
            AddHeader("Connection", "Upgrade");
            AddHeader(HeaderConstants.SEC_WEBSOCKET_VERSION, Version);
            _base64Key = createBase64Key();

            AddHeader(HeaderConstants.SEC_WEBSOCKET_KEY, _base64Key);
        }

        private static string createBase64Key()
        {
            var src = new byte[16];
            var rand = new Random();
            rand.NextBytes(src);

            return Convert.ToBase64String(src);
        }

        public string Base64Key { get { return _base64Key; } }

        //// As server
        //private bool isValidRequesHandshake1544955205()
        //{
        //    return !Context.IsValid
        //           ? false
        //           : !isValidHostHeader()
        //             ? false
        //             : Context.Headers.Exists("Sec-WebSocket-Version", _version);
        //}

        //public RequestHandshake CreateFromStream(Stream stream)
        //{
            
        //}

        public bool IsValid
        {
            get
            {
                return Headers.Exists(HeaderConstants.SEC_WEBSOCKET_VERSION, Version);
            }
        }

        public string HttpMethod { get; private set; }

        public bool IsWebSocketRequest
        {
            get
            {
                return HttpMethod != "GET"
                       && ProtocolVersion < HttpVersion.Version11
                       && !HeaderExists("Upgrade", "websocket")
                       && !HeaderExists("Connection", "Upgrade")
                       && !HeaderExists("Host")
                       && !HeaderExists(HeaderConstants.SEC_WEBSOCKET_KEY)
                       && HeaderExists(HeaderConstants.SEC_WEBSOCKET_VERSION);
            }
        }

        public NameValueCollection QueryString
        {
            get
            {
                if (_queryString == null)
                {
                    _queryString = new NameValueCollection();

                    var i = RawUrl.IndexOf('?');
                    if (i > 0)
                    {
                        var query = RawUrl.Substring(i + 1);
                        var components = query.Split('&');
                        foreach (var c in components)
                        {
                            var nv = c.GetNameAndValue("=");
                            if (nv.Key != null)
                            {
                                var name = nv.Key.UrlDecode();
                                var val = nv.Value.UrlDecode();
                                _queryString.Add(name, val);
                            }
                        }
                    }
                }

                return _queryString;
            }
        }

        public string RawUrl
        {
            get
            {
                return RequestUri.IsAbsoluteUri
                       ? RequestUri.PathAndQuery
                       : RequestUri.OriginalString;
            }
        }

        public Uri RequestUri { get; private set; }

        public static RequestHandshake Parse(string[] request)
        {
            var requestLine = request[0].Split(' ');
            if (requestLine.Length != 3)
            {
                var msg = "Invalid HTTP Request-Line: " + request[0];
                throw new ArgumentException(msg, "request");
            }

            var headers = new WebHeaderCollection();
            for (int i = 1; i < request.Length; i++)
                headers.SetInternal(request[i], false);

            var httpMethod = requestLine[0];
            var requestUri = requestLine[1].ToUri();
            var protocolVersion = new Version(requestLine[2].Substring(5));

            return new RequestHandshake(requestUri)
            {
                Headers = headers,
                HttpMethod = httpMethod,
                ProtocolVersion = protocolVersion
            };
        }

        public static RequestHandshake FromContext(WebSocketContext context)
        {
            return new RequestHandshake(context.RequestUri)
            {
                Headers = context.Headers,
                HttpMethod = "GET",
                ProtocolVersion = HttpVersion.Version11
            };
        }

        public void SetCookies(CookieCollection cookies)
        {
            if (cookies.IsNull() || cookies.Count == 0)
                return;

            var sorted = cookies.Sorted.ToArray();
            var header = new StringBuilder(sorted[0].ToString());
            for (int i = 1; i < sorted.Length; i++)
                if (!sorted[i].Expired)
                    header.AppendFormat("; {0}", sorted[i].ToString());

            AddHeader("Cookie", header.ToString());
        }

        public override string ToString()
        {
            var buffer = new StringBuilder();
            buffer.AppendFormat("{0} {1} HTTP/{2}{3}", HttpMethod, RawUrl, ProtocolVersion, _crlf);
            foreach (string key in Headers.AllKeys)
                buffer.AppendFormat("{0}: {1}{2}", key, Headers[key], _crlf);

            buffer.Append(_crlf);
            return buffer.ToString();
        }
    }
}
