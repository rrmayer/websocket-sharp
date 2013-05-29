
/*
 * WebSocketContext.cs
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
using System.Linq;
using System.Security.Principal;

namespace WebSocketSharp.Net.WebSockets
{

    /// <summary>
    /// Provides access to the WebSocket connection request objects.
    /// </summary>
    /// <remarks>
    /// The WebSocketContext class is an abstract class.
    /// </remarks>
    public abstract class WebSocketContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketSharp.Net.WebSockets.WebSocketContext"/> class.
        /// </summary>
        protected WebSocketContext()
        {
            //Id = Guid.NewGuid().ToString("N");
            //Properties = new Dictionary<string, object>();
        }



        /// <summary>
        /// Gets the cookies used in the WebSocket opening handshake.
        /// </summary>
        /// <value>
        /// A <see cref="WebSocketSharp.Net.CookieCollection"/> that contains the cookies.
        /// </value>
        protected abstract CookieCollection CookieCollection { get; }

        /// <summary>
        /// Gets the cookies used in the WebSocket opening handshake.
        /// </summary>
        /// <value>
        /// An IEnumerable&lt;Cookie&gt; interface that provides an enumerator which supports the iteration
        /// over the collection of cookies.
        /// </value>
        public IEnumerable<Cookie> Cookies
        {
            get
            {
                lock (CookieCollection.SyncRoot)
                {
                    return from Cookie cookie in CookieCollection
                           select cookie;
                }
            }
        }

        /// <summary>
        /// Gets the HTTP headers used in the WebSocket opening handshake.
        /// </summary>
        /// <value>
        /// A <see cref="System.Collections.Specialized.NameValueCollection"/> that contains the HTTP headers.
        /// </value>
        public abstract NameValueCollection Headers { get; }

        /// <summary>
        /// Gets a value indicating whether the client is authenticated.
        /// </summary>
        /// <value>
        /// <c>true</c> if the client is authenticated; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsAuthenticated { get; }

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection is secured.
        /// </summary>
        /// <value>
        /// <c>true</c> if the WebSocket connection is secured; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsSecure { get; }

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection request is valid.
        /// </summary>
        /// <value>
        /// <c>true</c> if the WebSocket connection request is valid; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsValid { get; }

        /// <summary>
        /// Gets the absolute path of the requested WebSocket URI.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that contains the absolute path of the requested WebSocket URI.
        /// </value>
        public string Path
        {
            get
            {
                return RequestUri == null ? null : RequestUri.GetAbsolutePath();
            }
        }

        /// <summary>
        /// Gets the collection of query string variables used in the WebSocket opening handshake.
        /// </summary>
        /// <value>
        /// A <see cref="NameValueCollection"/> that contains the collection of query string variables.
        /// </value>
        public abstract NameValueCollection QueryString { get; }

        /// <summary>
        /// Gets the WebSocket URI requested by the client.
        /// </summary>
        /// <value>
        /// A <see cref="RequestUri"/> that contains the WebSocket URI.
        /// </value>
        public abstract Uri RequestUri { get; }

        /// <summary>
        /// Gets the value of the Sec-WebSocket-Key header field used in the WebSocket opening handshake.
        /// </summary>
        /// <remarks>
        /// The SecWebSocketKey property provides a part of the information used by the server to prove that it received a valid WebSocket opening handshake.
        /// </remarks>
        /// <value>
        /// A <see cref="string"/> that contains the value of the Sec-WebSocket-Key header field.
        /// </value>
        public string SecWebSocketKey { get { return Headers[HeaderConstants.SEC_WEBSOCKET_KEY]; } }

        /// <summary>
        /// Gets the values of the Sec-WebSocket-Protocol header field used in the WebSocket opening handshake.
        /// </summary>
        /// <remarks>
        /// The SecWebSocketProtocols property indicates the subprotocols of the WebSocket connection.
        /// </remarks>
        /// <value>
        /// An IEnumerable&lt;string&gt; that contains the values of the Sec-WebSocket-Protocol header field.
        /// </value>
        public IEnumerable<string> SecWebSocketProtocols
        {
            get { return Headers.GetValues(HeaderConstants.SEC_WEBSOCKET_PROTOCOL); }
        }


        public IEnumerable<string> SecWebSocketExtensions
        {
            get { return Headers.GetValues(HeaderConstants.SEC_WEBSOCKET_EXTENSIONS); }
        }

        /// <summary>
        /// Gets the value of the Sec-WebSocket-Version header field used in the WebSocket opening handshake.
        /// </summary>
        /// <remarks>
        /// The SecWebSocketVersion property indicates the WebSocket protocol version of the connection.
        /// </remarks>
        /// <value>
        /// A <see cref="string"/> that contains the value of the Sec-WebSocket-Version header field.
        /// </value>
        public string SecWebSocketVersion
        {
            get
            {
                return Headers[HeaderConstants.SEC_WEBSOCKET_VERSION];
            }
        }

        /// <summary>
        /// Gets the value of the Origin header field used in the WebSocket opening handshake.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that contains the value of the Origin header field.
        /// </value>
        public string Origin
        {
            get
            {
                return Headers["Origin"];
            }
        }

        /// <summary>
        /// Gets the client information (identity, authentication information and security roles).
        /// </summary>
        /// <value>
        /// A <see cref="IPrincipal"/> that contains the client information.
        /// </value>
        public abstract IPrincipal User { get; }


    }
}
