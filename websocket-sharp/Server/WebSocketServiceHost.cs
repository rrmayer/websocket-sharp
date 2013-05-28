
/*
 * WebSocketServiceHost.cs
 *
 * A C# implementation of the WebSocket protocol server.
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


using System.Collections.Generic;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{

    /// <summary>
    /// Provides the functions of the server that receives the WebSocket connection requests.
    /// </summary>
    /// <remarks>
    /// The WebSocketServiceHost&lt;T&gt; class provides the single WebSocket service.
    /// </remarks>
    /// <typeparam name="T">
    /// The type of the WebSocket service that the server provides. The T must inherit the <see cref="WebSocketService"/> class.
    /// </typeparam>
    public class WebSocketServiceHost<T> : IWebSocketServiceHost
      where T : IWebSocketService, new()
    {

        private readonly WebSocketSessionManager _sessions;
        private readonly T _serviceImpl;

        /// <summary>
        /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for incoming connection attempts
        /// on the specified WebSocket URL.
        /// </summary>
        /// <param name="url">
        /// A <see cref="string"/> that contains a WebSocket service path.
        /// </param>
        public WebSocketServiceHost(string path)
        {
            _sessions = new WebSocketSessionManager();
            _serviceImpl = new T();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the server cleans up the inactive clients periodically.
        /// </summary>
        /// <value>
        /// <c>true</c> if the server cleans up the inactive clients every 60 seconds; otherwise, <c>false</c>.
        /// The default value is <c>true</c>.
        /// </value>
        public bool AutoCleanExpiredSessions
        {
            get
            {
                return _sessions.AutoCleanOldSessions;
            }

            set
            {
                _sessions.AutoCleanOldSessions = value;
            }
        }

        /// <summary>
        /// Gets the Path of the WebSocket URL on which to bind this service.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that contains a path.
        /// </value>
        public string Path { get; private set; }

        /// <summary>
        /// Accepts a WebSocket connection request.
        /// </summary>
        /// <param name="context">
        /// A <see cref="TcpListenerWebSocketContext"/> that contains the WebSocket connection request objects.
        /// </param>
        public void NewWebSocketClient(WebSocketContext context)
        {
            context.WebSocket.OnClose += (o, e) => _sessions.Remove(context.Id);
            context.WebSocket.OnClose += (o, e) => _serviceImpl.OnClose(context, e);
            context.WebSocket.OnMessage += (o, e) => _serviceImpl.OnMessage(context, e);
            context.WebSocket.OnError += (o, e) => _serviceImpl.OnError(context, e);

            _sessions.Add(context);
            _serviceImpl.OnOpen(context);
        }

        public IEnumerable<WebSocketContext> CurrentSessions { get { return _sessions.AllSessions; }}

            

        /// <summary>
        /// Broadcasts the specified <see cref="string"/> to all clients.
        /// </summary>
        /// <param name="data">
        /// A <see cref="string"/> to broadcast.
        /// </param>
        public void Broadcast(string data)
        {
            _sessions.Broadcast(data);
        }

        /// <summary>
        /// Broadcasts the specified <see cref="string"/> to all clients.
        /// </summary>
        /// <param name="data">
        /// A <see cref="byte"/> to broadcast.
        /// </param>
        public void Broadcast(byte[] data)
        {
            _sessions.Broadcast(data);
        }

        /// <summary>
        /// Pings with the specified <see cref="string"/> to all clients.
        /// </summary>
        /// <returns>
        /// A Dictionary&lt;string, bool&gt; that contains the collection of session IDs and values
        /// indicating whether the server received the Pongs from each clients in a time.
        /// </returns>
        /// <param name="message">
        /// A <see cref="string"/> that contains a message.
        /// </param>
        public Dictionary<string, bool> Broadping(string message)
        {
            return _sessions.Broadping(message);
        }

        
    }
}
