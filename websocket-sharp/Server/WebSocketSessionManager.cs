
/*
 * WebSocketServiceManager.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{

    /// <summary>
    /// Manages the collection of <see cref="WebSocketService"/> objects.
    /// </summary>
    public class WebSocketSessionManager
    {

        

        private readonly object _sweepLock;
        private volatile bool _isStopped;
        private volatile bool _isSweeping;
        private readonly object _syncRoot;
        private readonly ConcurrentDictionary<string, WebSocketContext> _contexts;
        private Timer _sweepTimer;

        

        

        internal WebSocketSessionManager()
        {
            _sweepLock = new object();
            _isStopped = false;
            _isSweeping = false;
            _contexts = new ConcurrentDictionary<string, WebSocketContext>();
            _syncRoot = new object();

            setSweepTimer();
            startSweepTimer();
        }

        

        

        /// <summary>
        /// Gets the collection of IDs of active <see cref="WebSocketService"/> objects
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <value>
        /// An IEnumerable&lt;string&gt; that contains the collection of IDs of active <see cref="WebSocketService"/> objects.
        /// </value>
        public IEnumerable<string> ActiveIDs
        {
            get
            {
                return from result in Broadping(String.Empty)
                       where result.Value
                       select result.Key;
            }
        }

        /// <summary>
        /// Gets a snapshot of all current sessions.
        /// </summary>
        public IEnumerable<WebSocketContext> AllSessions { get { return _contexts.Values; } }

        /// <summary>
        /// Gets the number of <see cref="WebSocketService"/> objects
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <value>
        /// An <see cref="int"/> that contains the number of <see cref="WebSocketService"/> objects
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </value>
        public int Count
        {
            get
            {
                return _contexts.Count;
            }
        }

        /// <summary>
        /// Gets the collection of IDs of inactive <see cref="WebSocketService"/> objects
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <value>
        /// An IEnumerable&lt;string&gt; that contains the collection of IDs of inactive <see cref="WebSocketService"/> objects.
        /// </value>
        public IEnumerable<string> InactiveIDs
        {
            get
            {
                return from result in Broadping(String.Empty)
                       where !result.Value
                       select result.Key;
            }
        }

        /// <summary>
        /// Gets the collection of IDs of <see cref="WebSocketService"/> objects
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <value>
        /// An IEnumerable&lt;string&gt; that contains the collection of IDs of <see cref="WebSocketService"/> objects.
        /// </value>
        public IEnumerable<string> IDs
        {
            get
            {
                return _contexts.Keys;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="WebSocketSessionManager"/> cleans up
        /// the inactive <see cref="WebSocketService"/> objects periodically.
        /// </summary>
        /// <value>
        /// <c>true</c> if the <see cref="WebSocketSessionManager"/> cleans up the inactive <see cref="WebSocketService"/> objects
        /// every 60 seconds; otherwise, <c>false</c>.
        /// </value>
        public bool AutoCleanOldSessions
        {
            get
            {
                return _sweepTimer.Enabled;
            }

            internal set
            {
                if (value && !_isStopped)
                    startSweepTimer();

                if (!value)
                    stopSweepTimer();
            }
        }

        

        

        private void broadcast(byte[] data)
        {
            lock (_syncRoot)
            {
                foreach (var c in _contexts.Values)
                    c.WebSocket.Send(data);
            }
        }

        private void broadcast(string data)
        {
            Parallel.ForEach(_contexts.Values, c => c.WebSocket.Send(data));
        }

        private void broadcastAsync(byte[] data, Action onComplete)
        {
            var copied = _contexts.Values;
            Task.Factory.StartNew(() =>
                                      {
                                          Parallel.ForEach(copied, i => i.WebSocket.Send(data));
                                          if (!onComplete.IsNull())
                                              onComplete();
                                      });
        }

        private void broadcastAsync(string data, Action onComplete)
        {
            var copied = _contexts.Values;
            Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(copied, i => i.WebSocket.Send(data));
                if (!onComplete.IsNull())
                    onComplete();
            });
        }

        private void setSweepTimer()
        {
            _sweepTimer = new Timer(60 * 1000);
            _sweepTimer.Elapsed += (sender, e) => Sweep();
        }

        private void startSweepTimer()
        {
            if (!AutoCleanOldSessions)
                _sweepTimer.Start();
        }

        private void closeAll(ushort code, string reason)
        {
            stopSweepTimer();
            lock (_syncRoot)
            {
                if (_isStopped)
                    return;

                _isStopped = true;
                Parallel.ForEach(_contexts.Values, context => context.WebSocket.Close(code, reason));
            }
        }

        private void stopSweepTimer()
        {
            if (AutoCleanOldSessions)
                _sweepTimer.Stop();
        }

        

        

        internal bool Add(WebSocketContext context)
        {
            if (_isStopped)
                return false;
            return _contexts.TryAdd(context.Id, context);
        }

        internal bool Remove(string id)
        {
            WebSocketContext context;
            return _contexts.TryRemove(id, out context);
        }

        

        

        /// <summary>
        /// Closes all sessions currently contained by this <see cref="WebSocketSesisonManager"/>
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        public void CloseAll(CloseStatusCode code, string reason)
        {
            closeAll((ushort)code, reason);
        }

        /// <summary>
        /// Broadcasts the specified array of <see cref="byte"/> to the clients of every <see cref="WebSocketService"/>
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <param name="data">
        /// An array of <see cref="byte"/> to broadcast.
        /// </param>
        public void Broadcast(byte[] data)
        {
            if (_isStopped)
                broadcast(data);
            else
                broadcastAsync(data, null);
        }

        /// <summary>
        /// Broadcasts the specified <see cref="string"/> to the clients of every <see cref="WebSocketService"/>
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <param name="data">
        /// A <see cref="string"/> to broadcast.
        /// </param>
        public void Broadcast(string data)
        {
            if (_isStopped)
                broadcast(data);
            else
                broadcastAsync(data, null);
        }

        /// <summary>
        /// Pings with the specified <see cref="string"/> to the clients of every <see cref="WebSocketService"/>
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <returns>
        /// A Dictionary&lt;string, bool&gt; that contains the collection of IDs and values
        /// indicating whether each <see cref="WebSocketService"/> received a Pong in a time.
        /// </returns>
        /// <param name="message">
        /// A <see cref="string"/> that contains a message.
        /// </param>
        public Dictionary<string, bool> Broadping(string message)
        {
            var result = new ConcurrentDictionary<string, bool>();
            Parallel.ForEach(_contexts.Values, context => result.TryAdd(context.Id, context.WebSocket.Ping(message)));

            return result.ToDictionary(a => a.Key, a => a.Value);
        }

        /// <summary>
        /// Cleans up the inactive <see cref="WebSocketService"/> objects.
        /// </summary>
        public void Sweep()
        {
            if (_isStopped || _isSweeping || Count == 0)
                return;

            lock (_sweepLock)
            {
                _isSweeping = true;
                foreach (var id in InactiveIDs)
                {
                    if (_isStopped)
                    {
                        _isSweeping = false;
                        return;
                    }

                    WebSocketContext context;
                    if (_contexts.TryRemove(id, out context))
                        context.WebSocket.Close(CloseStatusCode.NORMAL, "Session timeout");
                }

                _isSweeping = false;
            }
        }

        /// <summary>
        /// Tries to get the <see cref="WebSocketService"/> associated with the specified <paramref name="id"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="WebSocketSessionManager"/> manages the <see cref="WebSocketService"/> with the specified <paramref name="id"/>; otherwise, <c>false</c>.
        /// </returns>
        /// <param name="id">
        /// A <see cref="string"/> that contains the ID to find.
        /// </param>
        /// <param name="service">
        /// When this method returns, contains the <see cref="WebSocketService"/> with the specified <paramref name="id"/>, if the <paramref name="id"/> is found; otherwise, <see langword="null"/>.
        /// </param>
        public bool TryGetWebSocketService(string id, out WebSocketContext context)
        {
            return _contexts.TryGetValue(id, out context);
        }

        
    }
}
