#region License
/*
 * WebSocket.cs
 *
 * A C# implementation of the WebSocket interface.
 * This code derived from WebSocket.java (http://github.com/adamac/Java-WebSocket-client).
 *
 * The MIT License
 *
 * Copyright (c) 2009 Adam MacBeth
 * Copyright (c) 2010-2013 sta.blockhead
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
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;

namespace WebSocketSharp
{
    /// <summary>
    /// Implements the WebSocket interface.
    /// </summary>
    /// <remarks>
    /// The WebSocket class provides a set of methods and properties for two-way communication
    /// using the WebSocket protocol (<see href="http://tools.ietf.org/html/rfc6455">RFC 6455</see>).
    /// </remarks>
    public abstract class WebSocket : IDisposable
    {
        #region Private Const Fields

        private const int FragmentLen = 1016; // Max value is int.MaxValue - 14.

        #endregion

        #region Private Fields

        private bool _client;
        private CompressionMethod _compression;
        private string _extensions;
        private AutoResetEvent _exitReceiving;
        private Object _forClose;
        private Object _forSend;
        private string _origin;
        private bool _perFrameCompress;
        private volatile WebSocketState _state;
        private AutoResetEvent _receivePong;
        protected WsStream _wsStream;

        #endregion

        #region Private Constructors

        protected WebSocket()
        {
            _compression = CompressionMethod.NONE;
            _extensions = String.Empty;
            _forClose = new Object();
            _forSend = new Object();
            _origin = String.Empty;
            _perFrameCompress = false;
            _state = WebSocketState.CONNECTING;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Returns the stream established by the web sockets.
        /// </summary>
        protected Stream UnderlyingStream { get; set; }

        /// <summary>
        /// Called when this WebSocket is closing to free any resources.
        /// </summary>
        protected abstract void OnClosing();

        /// <summary>
        /// Gets or sets the compression method used to compress the payload data of the WebSocket Data frame.
        /// </summary>
        /// <value>
        /// One of the <see cref="CompressionMethod"/> values that indicates the compression method to use.
        /// The default is <see cref="CompressionMethod.NONE"/>.
        /// </value>
        public CompressionMethod Compression
        {
            get { return _compression; }

            set
            {
                if (isOpened(true))
                    return;

                _compression = value;
            }
        }

        /// <summary>
        /// Gets the WebSocket extensions selected by the server.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that contains the extensions if any. The default is <see cref="String.Empty"/>.
        /// </value>
        public string Extensions
        {
            get { return _extensions; }
        }

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection is alive by sending a Ping on the socket.
        /// </summary>
        /// <value>
        /// <c>true</c> if the connection is alive; otherwise, <c>false</c>.
        /// </value>
        public bool IsAlive
        {
            get
            {
                if (_state != WebSocketState.OPEN)
                    return false;

                return Ping();
            }
        }

        /// <summary>
        /// Gets or sets the value of the Origin header used in the WebSocket opening handshake.
        /// </summary>
        /// <remarks>
        /// A <see cref="WebSocket"/> instance does not send the Origin header in the WebSocket opening handshake
        /// if the value of this property is <see cref="String.Empty"/>.
        /// </remarks>
        /// <value>
        ///   <para>
        ///   A <see cref="string"/> that contains the value of the <see href="http://tools.ietf.org/html/rfc6454#section-7">HTTP Origin header</see> to send.
        ///   The default is <see cref="String.Empty"/>.
        ///   </para>
        ///   <para>
        ///   The value of the Origin header has the following syntax: <c>&lt;scheme&gt;://&lt;host&gt;[:&lt;port&gt;]</c>
        ///   </para>
        /// </value>
        public string Origin
        {
            get { return _origin; }

            set
            {
                if (isOpened(true))
                    return;

                if (value.IsNullOrEmpty())
                {
                    _origin = String.Empty;
                    return;
                }

                var origin = new Uri(value);
                if (!origin.IsAbsoluteUri || origin.Segments.Length > 1)
                {
                    onError("The syntax of value must be '<scheme>://<host>[:<port>]'.");
                    return;
                }

                _origin = value.TrimEnd('/');
            }
        }

        /// <summary>
        /// Gets the WebSocket subprotocol selected by the server.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that contains the subprotocol if any. The default is <see cref="String.Empty"/>.
        /// </value>
        public string SubProtocol {get; protected set;}

        /// <summary>
        /// Gets the current state of the WebSocket connection.
        /// </summary>
        /// <value>
        /// One of the <see cref="WebSocketState"/> values. The default is <see cref="WebSocketState.CONNECTING"/>.
        /// </value>
        public WebSocketState State { get; protected set; }


        #endregion

        #region Events

        /// <summary>
        /// Occurs when the <see cref="WebSocket"/> receives a Close frame or the Close method is called.
        /// </summary>
        public event EventHandler<CloseEventArgs> OnClose;

        /// <summary>
        /// Occurs when the <see cref="WebSocket"/> gets an error.
        /// </summary>
        public event EventHandler<ErrorEventArgs> OnError;

        /// <summary>
        /// Occurs when the <see cref="WebSocket"/> receives a data frame.
        /// </summary>
        public event EventHandler<MessageEventArgs> OnMessage;

        /// <summary>
        /// Occurs when the WebSocket connection has been established.
        /// </summary>
        public event EventHandler OnOpen;

        #endregion

        #region Private Methods

        protected static string createCompressExtension(CompressionMethod method)
        {
            return method != CompressionMethod.NONE
                   ? String.Format("permessage-compress; method={0}", method.ToString().ToLower())
                   : String.Empty;
        }

        private static WsFrame createControlFrame(Opcode opcode, PayloadData payloadData, bool client)
        {
            var mask = client ? Mask.MASK : Mask.UNMASK;
            return new WsFrame(opcode, mask, payloadData);
        }

        private static WsFrame createDataFrame(
          Fin fin, Opcode opcode, PayloadData payloadData, CompressionMethod method, bool compressed, bool client)
        {
            var mask = client ? Mask.MASK : Mask.UNMASK;
            var compress = compressed ? CompressionMethod.NONE : method;
            var frame = new WsFrame(fin, opcode, mask, payloadData, compress);
            if (compressed)
                frame.PerMessageCompressed = true;

            return frame;
        }

        protected bool isOpened(bool errorIfOpened)
        {
            if (_state != WebSocketState.OPEN && _state != WebSocketState.CLOSING)
                return false;

            if (errorIfOpened)
                onError("The WebSocket connection has been established already.");

            return true;
        }

        private void onClose(CloseEventArgs eventArgs)
        {
            if (!Thread.CurrentThread.IsBackground)
                if (!_exitReceiving.IsNull())
                    _exitReceiving.WaitOne(5 * 1000);

            if (!closeResources())
                eventArgs.WasClean = false;

            if (!eventArgs.IsNull())
                OnClose.Emit(this, eventArgs);
        }

        private bool closeResources()
        {
            State = WebSocketState.CLOSED;

            try
            {
                OnClosing();
                return true;
            }
            catch (Exception ex)
            {
                onError(ex.Message);
                return false;
            }
        }

        protected void onError(string message)
        {
            //#if DEBUG
            //            var callerFrame = new StackFrame(1);
            //            var caller = callerFrame.GetMethod();
            //            Console.WriteLine("WS: Error@{0}: {1}", caller.Name, message);
            //#endif

            OnError.Emit(this, new ErrorEventArgs(message));
        }

        private void onMessage(MessageEventArgs eventArgs)
        {
            if (!eventArgs.IsNull())
                OnMessage.Emit(this, eventArgs);
        }

        protected void onOpen()
        {
            _state = WebSocketState.OPEN;
            startReceiving();
            OnOpen.Emit(this, EventArgs.Empty);
        }

        private bool ping(string message, int millisecondsTimeout)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            if (buffer.Length > 125)
            {
                var msg = "The payload length of a Ping frame must be 125 bytes or less.";
                onError(msg);
                return false;
            }

            var frame = createControlFrame(Opcode.PING, new PayloadData(buffer), _client);
            if (!send(frame))
                return false;

            return _receivePong.WaitOne(millisecondsTimeout);
        }

        private void pong(PayloadData data)
        {
            var frame = createControlFrame(Opcode.PONG, data, _client);
            send(frame);
        }

        private void pong(string data)
        {
            var payloadData = new PayloadData(data);
            pong(payloadData);
        }

        private bool processAbnormal(WsFrame frame)
        {
            if (!frame.IsNull())
                return false;

#if DEBUG
            Console.WriteLine("WS: Info@processAbnormal: Start closing handshake.");
#endif
            var msg = "Failure to communicate in the WebSocket protocol.";
            Close(CloseStatusCode.ABNORMAL, msg);

            return true;
        }

        private bool processClose(WsFrame frame)
        {
            if (!frame.IsClose)
                return false;

#if DEBUG
            Console.WriteLine("WS: Info@processClose: Start closing handshake.");
#endif
            close(frame.PayloadData);

            return true;
        }

        private bool processData(WsFrame frame)
        {
            if (!frame.IsData)
                return false;

            if (frame.IsCompressed && _compression == CompressionMethod.NONE)
                return false;

            if (frame.IsCompressed)
                frame.Decompress(_compression);

            onMessage(new MessageEventArgs(frame.Opcode, frame.PayloadData));
            return true;
        }

        private bool processFragmented(WsFrame frame)
        {
            // Not first fragment
            if (frame.IsContinuation)
                return true;

            // Not fragmented
            if (frame.IsFinal)
                return false;

            // First fragment
            if (frame.IsData && !(frame.IsCompressed && _compression == CompressionMethod.NONE))
                processFragments(frame);
            else
                processIncorrectFrame();

            return true;
        }

        private void processFragments(WsFrame first)
        {
            bool compressed = first.IsCompressed;
            if (compressed && _perFrameCompress)
                first.Decompress(_compression);

            var buffer = new List<byte>(first.PayloadData.ToByteArray());
            Func<WsFrame, bool> processContinuation = contFrame =>
            {
                if (!contFrame.IsContinuation)
                    return false;

                if (contFrame.IsCompressed)
                    contFrame.Decompress(_compression);

                buffer.AddRange(contFrame.PayloadData.ToByteArray());
                return true;
            };

            while (true)
            {
                var frame = _wsStream.ReadFrame();
                if (processAbnormal(frame))
                    return;

                if (!frame.IsFinal)
                {
                    // MORE & CONT
                    if (processContinuation(frame))
                        continue;
                }
                else
                {
                    // FINAL & CONT
                    if (processContinuation(frame))
                        break;

                    // FINAL & PING
                    if (processPing(frame))
                        continue;

                    // FINAL & PONG
                    if (processPong(frame))
                        continue;

                    // FINAL & CLOSE
                    if (processClose(frame))
                        return;
                }

                // ?
                processIncorrectFrame();
                return;
            }

            var data = compressed && !_perFrameCompress
                     ? buffer.ToArray().Decompress(_compression)
                     : buffer.ToArray();

            onMessage(new MessageEventArgs(first.Opcode, new PayloadData(data)));
        }

        private void processFrame(WsFrame frame)
        {
            bool processed = processAbnormal(frame) ||
                             processFragmented(frame) ||
                             processData(frame) ||
                             processPing(frame) ||
                             processPong(frame) ||
                             processClose(frame);

            if (!processed)
                processIncorrectFrame();
        }

        private void processIncorrectFrame()
        {
#if DEBUG
            Console.WriteLine("WS: Info@processIncorrectFrame: Start closing handshake.");
#endif
            Close(CloseStatusCode.INCORRECT_DATA);
        }

        private bool processPing(WsFrame frame)
        {
            if (!frame.IsPing)
                return false;

#if DEBUG
            Console.WriteLine("WS: Info@processPing: Return Pong.");
#endif
            pong(frame.PayloadData);

            return true;
        }

        private bool processPong(WsFrame frame)
        {
            if (!frame.IsPong)
                return false;

#if DEBUG
            Console.WriteLine("WS: Info@processPong: Receive Pong.");
#endif
            _receivePong.Set();

            return true;
        }

        private bool send(WsFrame frame)
        {
            if (!isOpened(false))
            {
                onError("The WebSocket connection isn't established or has been closed.");
                return false;
            }

            try
            {
                if (_wsStream.IsNull())
                    return false;

                _wsStream.Write(frame);
                return true;
            }
            catch (Exception ex)
            {
                onError(ex.Message);
                return false;
            }
        }

        private void send(Opcode opcode, byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                send(opcode, ms);
            }
        }

        private void send(Opcode opcode, Stream stream)
        {
            if (_compression == CompressionMethod.NONE || _perFrameCompress)
            {
                send(opcode, stream, false);
                return;
            }

            using (var compressed = stream.Compress(_compression))
            {
                send(opcode, compressed, true);
            }
        }

        private void send(Opcode opcode, Stream stream, bool compressed)
        {
            lock (_forSend)
            {
                try
                {
                    if (_state != WebSocketState.OPEN)
                    {
                        onError("The WebSocket connection isn't established or has been closed.");
                        return;
                    }

                    var length = stream.Length;
                    if (length <= FragmentLen)
                        send(Fin.FINAL, opcode, stream.ReadBytes((int)length), compressed);
                    else
                        sendFragmented(opcode, stream, compressed);
                }
                catch (Exception ex)
                {
                    onError(ex.Message);
                }
            }
        }

        private bool send(Fin fin, Opcode opcode, byte[] data, bool compressed)
        {
            var frame = createDataFrame(
              fin, opcode, new PayloadData(data), _compression, compressed, _client);
            return send(frame);
        }

        private void sendAsync(Opcode opcode, byte[] data, Action completed)
        {
            sendAsync(opcode, new MemoryStream(data), completed);
        }

        private void sendAsync(Opcode opcode, Stream stream, Action completed)
        {
            Action<Opcode, Stream> action = send;

            AsyncCallback callback = (ar) =>
            {
                try
                {
                    action.EndInvoke(ar);
                    if (!completed.IsNull())
                        completed();
                }
                catch (Exception ex)
                {
                    onError(ex.Message);
                }
                finally
                {
                    stream.Close();
                }
            };

            action.BeginInvoke(opcode, stream, callback, null);
        }

        private void sendFragmented(Opcode opcode, Stream stream, bool compressed)
        {
            var length = stream.Length;
            var quo = length / FragmentLen;
            var rem = length % FragmentLen;
            var count = rem == 0 ? quo - 2 : quo - 1;

            long readLen = 0;
            var tmpLen = 0;
            var buffer = new byte[FragmentLen];

            // First
            tmpLen = stream.Read(buffer, 0, FragmentLen);
            if (send(Fin.MORE, opcode, buffer, compressed))
                readLen += tmpLen;
            else
                return;

            // Mid
            for (long i = 0; i < count; i++)
            {
                tmpLen = stream.Read(buffer, 0, FragmentLen);
                if (send(Fin.MORE, Opcode.CONT, buffer, compressed))
                    readLen += tmpLen;
                else
                    return;
            }

            // Final
            if (rem != 0)
                buffer = new byte[rem];
            tmpLen = stream.Read(buffer, 0, buffer.Length);
            if (send(Fin.FINAL, Opcode.CONT, buffer, compressed))
                readLen += tmpLen;
        }

        private void startReceiving()
        {
            _exitReceiving = new AutoResetEvent(false);
            _receivePong = new AutoResetEvent(false);

            Action<WsFrame> completed = null;
            completed = (frame) =>
            {
                try
                {
                    processFrame(frame);
                    if (_state == WebSocketState.OPEN)
                        _wsStream.ReadFrameAsync(completed);
                    else
                        _exitReceiving.Set();
                }
                catch (WsReceivedTooBigMessageException ex)
                {
                    Close(CloseStatusCode.TOO_BIG, ex.Message);
                }
                catch (Exception)
                {
                    Close(CloseStatusCode.ABNORMAL, "An exception has occured.");
                }
            };

            _wsStream.ReadFrameAsync(completed);
        }

        private void close(ushort code, string reason)
        {
            var data = code.Append(reason);
            if (data.Length > 125)
            {
                onError("The payload length of a Close frame must be 125 bytes or less.");
                return;
            }

            close(new PayloadData(data));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Closes the WebSocket connection and releases all associated resources.
        /// </summary>
        public void Close()
        {
            close(new PayloadData());
        }

        private void close(PayloadData data)
        {
            //Console.WriteLine("WS: Info@close: Current thread IsBackground?: {0}", Thread.CurrentThread.IsBackground);

            lock (_forClose)
            {
                // Whether the closing handshake has been started already?
                if (State == WebSocketState.CLOSING || State == WebSocketState.CLOSED)
                    return;

                // Whether the closing handshake on server is started before the connection has been established?
                if (State == WebSocketState.CONNECTING && !_client)
                {
                    sendResponseHandshake(HttpStatusCode.BadRequest);
                    onClose(new CloseEventArgs(data));

                    return;
                }

                State = WebSocketState.CLOSING;
            }

            // Whether a payload data contains the close status code which must not be set for send?
            if (data.ContainsReservedCloseStatusCode)
            {
                onClose(new CloseEventArgs(data));
                return;
            }

            closeHandshake(data);

            //Console.WriteLine("WS: Info@close: Exit close method.");
        }

        /// <summary>
        /// Closes the WebSocket connection with the specified <paramref name="code"/> and
        /// releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This Close method emits a <see cref="OnError"/> event if <paramref name="code"/> is not
        /// in the allowable range of the WebSocket close status code.
        /// </remarks>
        /// <param name="code">
        /// A <see cref="ushort"/> that indicates the status code for closure.
        /// </param>
        public void Close(ushort code)
        {
            Close(code, String.Empty);
        }

        /// <summary>
        /// Closes the WebSocket connection with the specified <paramref name="code"/> and
        /// releases all associated resources.
        /// </summary>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> values that indicates the status code for closure.
        /// </param>
        public void Close(CloseStatusCode code)
        {
            close((ushort)code, String.Empty);
        }

        /// <summary>
        /// Closes the WebSocket connection with the specified <paramref name="code"/> and
        /// <paramref name="reason"/>, and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This Close method emits a <see cref="OnError"/> event if <paramref name="code"/> is not
        /// in the allowable range of the WebSocket close status code.
        /// </remarks>
        /// <param name="code">
        /// A <see cref="ushort"/> that indicates the status code for closure.
        /// </param>
        /// <param name="reason">
        /// A <see cref="string"/> that contains the reason for closure.
        /// </param>
        public void Close(ushort code, string reason)
        {
            if (!code.IsCloseStatusCode())
            {
                var msg = String.Format("Invalid close status code: {0}", code);
                throw new ArgumentException(msg, "code");
            }

            close(code, reason);
        }

        /// <summary>
        /// Closes the WebSocket connection with the specified <paramref name="code"/> and
        /// <paramref name="reason"/>, and releases all associated resources.
        /// </summary>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> values that indicates the status code for closure.
        /// </param>
        /// <param name="reason">
        /// A <see cref="string"/> that contains the reason for closure.
        /// </param>
        public void Close(CloseStatusCode code, string reason)
        {
            close((ushort)code, reason);
        }

        /// <summary>
        /// Closes the WebSocket connection and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method closes the WebSocket connection with the <see cref="CloseStatusCode.AWAY"/>.
        /// </remarks>
        public void Dispose()
        {
            Close(CloseStatusCode.AWAY);
        }

        /// <summary>
        /// Pings using the WebSocket connection.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="WebSocket"/> receives a Pong in a time; otherwise, <c>false</c>.
        /// </returns>
        public bool Ping()
        {
            return Ping(String.Empty);
        }

        /// <summary>
        /// Pings with the specified <paramref name="message"/> using the WebSocket connection.
        /// </summary>
        /// <param name="message">
        /// A <see cref="string"/> that contains a message.
        /// </param>
        /// <returns>
        /// <c>true</c> if the <see cref="WebSocket"/> receives a Pong in a time; otherwise, <c>false</c>.
        /// </returns>
        public bool Ping(string message)
        {
            if (message.IsNull())
                message = String.Empty;

            return _client
                   ? ping(message, 5 * 1000)
                   : ping(message, 1 * 1000);
        }

        /// <summary>
        /// Sends a binary <paramref name="data"/> using the WebSocket connection.
        /// </summary>
        /// <param name="data">
        /// An array of <see cref="byte"/> that contains a binary data to send.
        /// </param>
        public void Send(byte[] data)
        {
            if (data.IsNull())
                throw new ArgumentNullException("data");

            send(Opcode.BINARY, data);
        }

        /// <summary>
        /// Sends a text <paramref name="text"/> using the WebSocket connection.
        /// </summary>
        /// <param name="text">
        /// A <see cref="string"/> that contains a text data to send.
        /// </param>
        public void Send(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentNullException("text");

            var buffer = Encoding.UTF8.GetBytes(text);
            send(Opcode.TEXT, buffer);
        }

        /// <summary>
        /// Sends a binary data using the WebSocket connection.
        /// </summary>
        /// <param name="file">
        /// A <see cref="FileInfo"/> that contains a binary data to send.
        /// </param>
        public void Send(FileInfo file)
        {
            if (file.IsNull())
                throw new ArgumentNullException("file");

            using (var fs = file.OpenRead())
            {
                send(Opcode.BINARY, fs);
            }
        }

        /// <summary>
        /// Sends a binary <paramref name="data"/> asynchronously using the WebSocket connection.
        /// </summary>
        /// <param name="data">
        /// An array of <see cref="byte"/> that contains a binary data to send.
        /// </param>
        /// <param name="completed">
        /// An <see cref="Action"/> delegate that references the method(s) called when
        /// the asynchronous operation completes.
        /// </param>
        public void SendAsync(byte[] data, Action completed)
        {
            if (data.IsNull())
                throw new ArgumentNullException("data");

            sendAsync(Opcode.BINARY, data, completed);
        }

        /// <summary>
        /// Sends a text <paramref name="data"/> asynchronously using the WebSocket connection.
        /// </summary>
        /// <param name="data">
        /// A <see cref="string"/> that contains a text data to send.
        /// </param>
        /// <param name="completed">
        /// An <see cref="Action"/> delegate that references the method(s) called when
        /// the asynchronous operation completes.
        /// </param>
        public void SendAsync(string data, Action completed)
        {
            if (data.IsNull())
                throw new ArgumentNullException("data");

            var buffer = Encoding.UTF8.GetBytes(data);
            sendAsync(Opcode.TEXT, buffer, completed);
        }

        /// <summary>
        /// Sends a binary data asynchronously using the WebSocket connection.
        /// </summary>
        /// <param name="file">
        /// A <see cref="FileInfo"/> that contains a binary data to send.
        /// </param>
        /// <param name="completed">
        /// An <see cref="Action"/> delegate that references the method(s) called when
        /// the asynchronous operation completes.
        /// </param>
        public void SendAsync(FileInfo file, Action completed)
        {
            if (file.IsNull())
                throw new ArgumentNullException("file");

            sendAsync(Opcode.BINARY, file.OpenRead(), completed);
        }

        #endregion

        //private bool isValidHostHeader()
        //{
        //    var authority = _context.Headers["Host"];
        //    if (authority.IsNullOrEmpty() || !_uri.IsAbsoluteUri)
        //        return true;

        //    var i = authority.IndexOf(':');
        //    var host = i > 0
        //                   ? authority.Substring(0, i)
        //                   : authority;
        //    var type = Uri.CheckHostName(host);

        //    return type != UriHostNameType.Dns
        //               ? true
        //               : Uri.CheckHostName(_uri.DnsSafeHost) != UriHostNameType.Dns
        //                     ? true
        //                     : host == _uri.DnsSafeHost;
        //}

        //private bool isValidRequesHandshake()
        //{
        //    return !_context.IsValid
        //               ? false
        //               : !isValidHostHeader()
        //                     ? false
        //                     : _context.Headers.Exists("Sec-WebSocket-Version", _version);
        //}
    }
}
