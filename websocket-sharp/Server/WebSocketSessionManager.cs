
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
    /// Manages the collection of <see cref="ServerSession"/> objects.
    /// </summary>
    public sealed class WebSocketSessionManager
    {
        private readonly object _sweepLock;
        private volatile bool _isStopped;
        private volatile bool _isSweeping;
        private readonly object _syncRoot;
        private readonly ConcurrentDictionary<Guid, ServerSession> _sessions;
        private Timer _sweepTimer;

        internal WebSocketSessionManager()
        {
            _sweepLock = new object();
            _isStopped = false;
            _isSweeping = false;
            _sessions = new ConcurrentDictionary<Guid, ServerSession>();
            _syncRoot = new object();

            setSweepTimer();
            startSweepTimer();
        }

        /// <summary>
        /// Gets the collection of IDs of active <see cref="ServerSession"/> objects
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <value>
        /// An IEnumerable&lt;string&gt; that contains the collection of IDs of active <see cref="ServerSession"/> objects.
        /// </value>
        public IEnumerable<Guid> ActiveIDs
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
        public IEnumerable<ServerSession> AllSessions { get { return _sessions.Values; } }

        /// <summary>
        /// Gets the number of <see cref="ServerSession"/> objects
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <value>
        /// An <see cref="int"/> that contains the number of <see cref="ServerSession"/> objects
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </value>
        public int Count
        {
            get
            {
                return _sessions.Count;
            }
        }

        /// <summary>
        /// Gets the collection of IDs of inactive <see cref="ServerSession"/> objects
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <value>
        /// An IEnumerable&lt;string&gt; that contains the collection of IDs of inactive <see cref="ServerSession"/> objects.
        /// </value>
        public IEnumerable<Guid> InactiveIDs
        {
            get
            {
                return from result in Broadping(String.Empty)
                       where !result.Value
                       select result.Key;
            }
        }

        /// <summary>
        /// Gets the collection of IDs of <see cref="ServerSession"/> objects
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <value>
        /// An IEnumerable&lt;string&gt; that contains the collection of IDs of <see cref="ServerSession"/> objects.
        /// </value>
        public IEnumerable<Guid> IDs
        {
            get
            {
                return _sessions.Keys;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="WebSocketSessionManager"/> cleans up
        /// the inactive <see cref="ServerSession"/> objects periodically.
        /// </summary>
        /// <value>
        /// <c>true</c> if the <see cref="WebSocketSessionManager"/> cleans up the inactive <see cref="ServerSession"/> objects
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
                foreach (var c in _sessions.Values)
                    c.WebSocket.Send(data);
            }
        }

        private void broadcast(string data)
        {
            Parallel.ForEach(_sessions.Values, c => c.WebSocket.Send(data));
        }

        private void broadcastAsync(byte[] data, Action onComplete)
        {
            var copied = _sessions.Values;
            Task.Factory.StartNew(() =>
                                      {
                                          Parallel.ForEach(copied, i => i.WebSocket.Send(data));
                                          if (!onComplete.IsNull())
                                              onComplete();
                                      });
        }

        private void broadcastAsync(string data, Action onComplete)
        {
            var copied = _sessions.Values;
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
                Parallel.ForEach(_sessions.Values, context => context.WebSocket.Close(code, reason));
            }
        }

        private void stopSweepTimer()
        {
            if (AutoCleanOldSessions)
                _sweepTimer.Stop();
        }

        internal bool Add(ServerSession context)
        {
            return !_isStopped && _sessions.TryAdd(context.Id, context);
        }

        internal bool Remove(Guid id)
        {
            ServerSession context;
            var removed = _sessions.TryRemove(id, out context);
            if (removed)
                context.Dispose();
            return removed;
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
        /// Broadcasts the specified array of <see cref="byte"/> to the clients of every <see cref="ServerSession"/>
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
        /// Broadcasts the specified <see cref="string"/> to the clients of every <see cref="ServerSession"/>
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
        /// Pings with the specified <see cref="string"/> to the clients of every <see cref="ServerSession"/>
        /// managed by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        /// <returns>
        /// A Dictionary&lt;string, bool&gt; that contains the collection of IDs and values
        /// indicating whether each <see cref="ServerSession"/> received a Pong in a time.
        /// </returns>
        /// <param name="message">
        /// A <see cref="string"/> that contains a message.
        /// </param>
        public Dictionary<Guid, bool> Broadping(string message)
        {
            var result = new ConcurrentDictionary<Guid, bool>();
            Parallel.ForEach(_sessions.Values, context => result.TryAdd(context.Id, context.WebSocket.Ping(message)));

            return result.ToDictionary(a => a.Key, a => a.Value);
        }

        /// <summary>
        /// Cleans up the inactive <see cref="ServerSession"/> objects.
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

                    ServerSession session;
                    if (_sessions.TryRemove(id, out session))
                    {
                        session.WebSocket.Close(CloseStatusCode.NORMAL, "Session timeout");
                        session.Dispose();
                    }

                    _isSweeping = false;
                }
            }
        }

        /// <summary>
        /// Tries to get the <see cref="ServerSession"/> associated with the specified <paramref name="id"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="WebSocketSessionManager"/> manages the <see cref="ServerSession"/> with the specified <paramref name="id"/>; otherwise, <c>false</c>.
        /// </returns>
        /// <param name="id">
        /// A <see cref="string"/> that contains the ID to find.
        /// </param>
        /// <param name="service">
        /// When this method returns, contains the <see cref="ServerSession"/> with the specified <paramref name="id"/>, if the <paramref name="id"/> is found; otherwise, <see langword="null"/>.
        /// </param>
        public bool TryGetWebSocketSession(Guid id, out ServerSession session)
        {
            return _sessions.TryGetValue(id, out session);
        }


    }
}
