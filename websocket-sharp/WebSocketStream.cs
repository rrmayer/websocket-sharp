
/*
 * WsStream.cs
 *
 * The MIT License
 *
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


using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebSocketSharp.Net.Security;

namespace WebSocketSharp
{
    internal class WebSocketStream : Stream, IDisposable
    {
        

        private Stream _innerStream;
        private bool _isSecure;
        private Object _forRead;
        private Object _forWrite;

        

        

        private WebSocketStream()
        {
            _forRead = new object();
            _forWrite = new object();
        }

        

        

        public WebSocketStream(NetworkStream innerStream)
            : this()
        {
            if (innerStream.IsNull())
                throw new ArgumentNullException("innerStream");

            _innerStream = innerStream;
            _isSecure = false;
        }

        public WebSocketStream(SslStream innerStream)
            : this()
        {
            if (innerStream.IsNull())
                throw new ArgumentNullException("innerStream");

            _innerStream = innerStream;
            _isSecure = true;
        }

        

        

        public bool DataAvailable
        {
            get
            {
                return _isSecure
                       ? ((SslStream)_innerStream).DataAvailable
                       : ((NetworkStream)_innerStream).DataAvailable;
            }
        }

        public bool IsSecure
        {
            get { return _innerStream is SslStream; }
        }

        

        

        public override int Read(byte[] buffer, int offset, int size)
        {
            var readLen = _innerStream.Read(buffer, offset, size);
            if (readLen < size)
            {
                var msg = String.Format("Data can not be read from {0}.", _innerStream.GetType().Name);
                throw new IOException(msg);
            }

            return readLen;
        }

        public override int ReadByte()
        {
            return _innerStream.ReadByte();
        }

        private string[] readHandshake()
        {
            var buffer = new List<byte>();
            while (true)
            {
                if (ReadByte().EqualsAndSaveTo('\r', buffer) &&
                    ReadByte().EqualsAndSaveTo('\n', buffer) &&
                    ReadByte().EqualsAndSaveTo('\r', buffer) &&
                    ReadByte().EqualsAndSaveTo('\n', buffer))
                    break;
            }

            return Encoding.UTF8.GetString(buffer.ToArray())
                   .Replace("\r\n", "\n").Replace("\n\n", "\n").TrimEnd('\n')
                   .Split('\n');
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_forWrite)
            {
                _innerStream.Write(buffer, offset, count);
            }
        }

        public override void WriteByte(byte value)
        {
            lock (_forWrite)
            {
                _innerStream.WriteByte(value);
            }
        }

        public void WriteBytes(byte[] buffer)
        {
            Write(buffer, 0, buffer.Length);
        }

        public override bool CanRead
        {
            get { return _innerStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _innerStream.CanSeek; }
        }

        public override bool CanTimeout
        {
            get { return _innerStream.CanTimeout; }
        }

        public override bool CanWrite
        {
            get { return _innerStream.CanWrite; }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _innerStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _innerStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void Flush()
        {
            _innerStream.Flush();
        }

        public override long Length
        {
            get { return _innerStream.Length; }
        }

        public override long Position
        {
            get { return _innerStream.Position; }
            set
            { _innerStream.Position = value; }
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return _innerStream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _innerStream.EndWrite(asyncResult);
        }

        public override int ReadTimeout
        {
            get
            {
                return _innerStream.ReadTimeout;
            }
            set
            {
                _innerStream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return _innerStream.WriteTimeout;
            }
            set
            {
                _innerStream.WriteTimeout = value;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        

        

        internal static WebSocketStream CreateClientStream(TcpClient client, string host, bool secure)
        {
            var netStream = client.GetStream();
            if (secure)
            {
                System.Net.Security.RemoteCertificateValidationCallback validationCb = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // FIXME: Always returns true
                    return true;
                };

                var sslStream = new SslStream(netStream, false, validationCb);
                sslStream.AuthenticateAsClient(host);

                return new WebSocketStream(sslStream);
            }

            return new WebSocketStream(netStream);
        }

        internal static WebSocketStream CreateServerStream(TcpClient client, bool secure)
        {
            var netStream = client.GetStream();
            if (secure)
            {
                var sslStream = new SslStream(netStream, false);
                var certPath = ConfigurationManager.AppSettings["ServerCertPath"];
                sslStream.AuthenticateAsServer(new X509Certificate2(certPath));

                return new WebSocketStream(sslStream);
            }

            return new WebSocketStream(netStream);
        }

        internal static WebSocketStream CreateServerStream(WebSocketSharp.Net.HttpListenerContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            var conn = context.Connection;
            var stream = conn.Stream;

            return conn.IsSecure
                   ? new WebSocketStream((SslStream)stream)
                   : new WebSocketStream((NetworkStream)stream);
        }

        

        

        public override void Close()
        {
            _innerStream.Close();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _innerStream != null)
                _innerStream.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        internal WebSocketFrame ReadFrame()
        {
            lock (_forRead)
            {
                try
                {
                    return WebSocketFrame.Parse(_innerStream);
                }
                catch
                {
                    return null;
                }
            }
        }

        internal void ReadFrameAsync(Action<WebSocketFrame> completed)
        {
            WebSocketFrame.ParseAsync(_innerStream, completed);
        }

        internal string[] ReadHandshake()
        {
            lock (_forRead)
            {
                try
                {
                    return readHandshake();
                }
                catch
                {
                    return null;
                }
            }
        }

        internal string[] ReadHandshake(TimeSpan timeout)
        {

        }

        internal void Write(WebSocketFrame frame)
        {
            var bytes = frame.ToByteArray();
            WriteBytes(bytes);
        }

        internal void Write(Handshake handshake)
        {
            var bytes = handshake.ToBytes();
            WriteBytes(bytes);
        }

        
    }
}
