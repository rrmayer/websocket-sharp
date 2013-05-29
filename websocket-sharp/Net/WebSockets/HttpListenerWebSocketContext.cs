
/*
 * HttpListenerWebSocketContext.cs
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
using System.Security.Principal;

namespace WebSocketSharp.Net.WebSockets
{

    /// <summary>
    /// Provides access to the WebSocket connection request objects received by the <see cref="HttpListener"/> class.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class HttpListenerWebSocketContext : WebSocketContext
    {
        private readonly HttpListenerContext _context;

        internal HttpListenerWebSocketContext(HttpListenerContext context)
        {
            _context = context;
        }
        

        /// <summary>
        /// Gets the cookies used in the WebSocket opening handshake.
        /// </summary>
        /// <value>
        /// A <see cref="WebSocketSharp.Net.CookieCollection"/> that contains the cookies.
        /// </value>
        protected override CookieCollection CookieCollection
        {
            get
            {
                return _context.Request.Cookies;
            }
        }

        /// <summary>
        /// Gets the HTTP headers used in the WebSocket opening handshake.
        /// </summary>
        /// <value>
        /// A <see cref="System.Collections.Specialized.NameValueCollection"/> that contains the HTTP headers.
        /// </value>
        public override NameValueCollection Headers
        {
            get
            {
                return _context.Request.Headers;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the client is authenticated.
        /// </summary>
        /// <value>
        /// <c>true</c> if the client is authenticated; otherwise, <c>false</c>.
        /// </value>
        public override bool IsAuthenticated
        {
            get
            {
                return _context.Request.IsAuthenticated;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection is secured.
        /// </summary>
        /// <value>
        /// <c>true</c> if the WebSocket connection is secured; otherwise, <c>false</c>.
        /// </value>
        public override bool IsSecure
        {
            get
            {
                return _context.Request.IsSecureConnection;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection request is valid.
        /// </summary>
        /// <value>
        /// <c>true</c> if the WebSocket connection request is valid; otherwise, <c>false</c>.
        /// </value>
        public override bool IsValid
        {
            get
            {
                return _context.Request.IsWebSocketRequest && (!SecWebSocketKey.IsNullOrEmpty() && !SecWebSocketVersion.IsNullOrEmpty());
            }
        }

        /// <summary>
        /// Gets the collection of query string variables used in the WebSocket opening handshake.
        /// </summary>
        /// <value>
        /// A <see cref="NameValueCollection"/> that contains the collection of query string variables.
        /// </value>
        public override NameValueCollection QueryString
        {
            get
            {
                return _context.Request.QueryString;
            }
        }

        /// <summary>
        /// Gets the WebSocket URI requested by the client.
        /// </summary>
        /// <value>
        /// A <see cref="RequestUri"/> that contains the WebSocket URI.
        /// </value>
        public override Uri RequestUri
        {
            get
            {
                return _context.Request.RawUrl.ToUri();
            }
        }

        /// <summary>
        /// Gets the server endpoint as an IP address and a port number.
        /// </summary>
        /// <value>
        /// A <see cref="System.Net.IPEndPoint"/> that contains the server endpoint.
        /// </value>
        public virtual System.Net.IPEndPoint ServerEndPoint
        {
            get
            {
                return _context.Connection.LocalEndPoint;
            }
        }

        /// <summary>
        /// Gets the client information (identity, authentication information and security roles).
        /// </summary>
        /// <value>
        /// A <see cref="IPrincipal"/> that contains the client information.
        /// </value>
        public override IPrincipal User
        {
            get
            {
                return _context.User;
            }
        }

        /// <summary>
        /// Gets the client endpoint as an IP address and a port number.
        /// </summary>
        /// <value>
        /// A <see cref="System.Net.IPEndPoint"/> that contains the client endpoint.
        /// </value>
        public virtual System.Net.IPEndPoint RemoteEndPoint
        {
            get
            {
                return _context.Connection.RemoteEndPoint;
            }
        }
    }
}
