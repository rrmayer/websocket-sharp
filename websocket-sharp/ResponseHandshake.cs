
/*
 * ResponseHandshake.cs
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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using WebSocketSharp.Net;

namespace WebSocketSharp
{
    internal class ResponseHandshake : Handshake
    {
        private const string Guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private ResponseHandshake()
            : this(HttpStatusCode.SwitchingProtocols)
        {
            AddHeader("Upgrade", "websocket");
            AddHeader("Connection", "Upgrade");
        }

        public ResponseHandshake(string base64Key)
            : this()
        {
            AddHeader(HeaderConstants.SEC_WEBSOCKET_ACCEPT, createResponseKey(base64Key));
        }

        public bool IsCorrespondingResponse(RequestHandshake handshake)
        {
            return IsWebSocketResponse
                    && HeaderExists(HeaderConstants.SEC_WEBSOCKET_ACCEPT, createResponseKey(handshake.Base64Key))
                    && (!HeaderExists(HeaderConstants.SEC_WEBSOCKET_VERSION) || HeaderExists(HeaderConstants.SEC_WEBSOCKET_VERSION, RequestHandshake.Version));
        }

        public string SubProtocol
        {
            get { return Headers[HeaderConstants.SEC_WEBSOCKET_PROTOCOL]; }
        }

        public string Extensions
        {
            get { return Headers[HeaderConstants.SEC_WEBSOCKET_EXTENSIONS]; }
        }


        private string createResponseKey(string base64Key)
        {
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            var sb = new StringBuilder(base64Key);
            sb.Append(ResponseHandshake.Guid);
            var src = sha1.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));

            return Convert.ToBase64String(src);
        }

        private ResponseHandshake(HttpStatusCode code)
        {
            StatusCode = ((int)code).ToString();
            Reason = code.GetDescription();
            AddHeader("Server", "websocket-sharp/1.0");
        }

        public CookieCollection Cookies
        {
            get
            {
                return Headers.GetCookies(true);
            }
        }

        public bool IsWebSocketResponse
        {
            get
            {
                return ProtocolVersion >= HttpVersion.Version11
                    && StatusCode == "101"
                    && HeaderExists("Upgrade", "websocket")
                    && HeaderExists("Connection", "Upgrade")
                    && HeaderExists(HeaderConstants.SEC_WEBSOCKET_ACCEPT);
            }
        }

        public string Reason { get; internal set; }

        public string StatusCode { get; internal set; }

        public static ResponseHandshake CreateCloseResponse(HttpStatusCode code)
        {
            var res = new ResponseHandshake(code);
            res.AddHeader("Connection", "close");
            res.AddHeader(HeaderConstants.SEC_WEBSOCKET_VERSION, RequestHandshake.Version);

            return res;
        }

        public static ResponseHandshake ReadFromStream(WebSocketStream responseStream)
        {
            var response = responseStream.ReadHandshake(TimeSpan.FromSeconds(30));

            var statusLine = response[0].Split(' ');
            if (statusLine.Length < 3)
                throw new ArgumentException("Invalid status line.");

            var reason = new StringBuilder(statusLine[2]);
            for (int i = 3; i < statusLine.Length; i++)
                reason.AppendFormat(" {0}", statusLine[i]);

            var headers = new WebHeaderCollection();
            for (int i = 1; i < response.Length; i++)
                headers.SetInternal(response[i], true);

            return new ResponseHandshake
            {
                Headers = headers,
                Reason = reason.ToString(),
                StatusCode = statusLine[1],
                ProtocolVersion = new Version(statusLine[0].Substring(5))
            };
        }



        public void SetCookies(CookieCollection cookies)
        {
            if (cookies.IsNull() || cookies.Count == 0)
                return;

            foreach (var cookie in cookies.Sorted)
                AddHeader("Set-Cookie", cookie.ToResponseString());
        }

        public override string ToString()
        {
            var buffer = new StringBuilder();
            buffer.AppendFormat("HTTP/{0} {1} {2}{3}", ProtocolVersion, StatusCode, Reason, _crlf);
            foreach (string key in Headers.AllKeys)
                buffer.AppendFormat("{0}: {1}{2}", key, Headers[key], _crlf);

            buffer.Append(_crlf);
            return buffer.ToString();
        }


    }
}
