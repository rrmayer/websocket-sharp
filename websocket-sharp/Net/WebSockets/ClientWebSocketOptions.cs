using System;
using System.Collections.Generic;
using System.Net;

namespace WebSocketSharp.Net.WebSockets
{
    public class ClientWebSocketOptions
    {
        //public X509CertificateCollection ClientCertificates { get; set; }
        /// <summary>
        /// Gets or sets the cookies associated with the request.
        /// </summary>
        public CookieContainer Cookies { get; set; }

        /// <summary>
        /// Gets or sets the credential information for the client.
        /// </summary>
        public ICredentials Credentials { get; set; }

        /// <summary>
        /// Gets or sets the WebSocket protocol keep-alive interval in milliseconds.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; }

        /// <summary>
        /// Gets or sets the proxy for WebSocket requests.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets a Boolean value that indicates if default credentials should be used during WebSocket handshake.
        /// </summary>
        //public bool UseDefaultCredentials { get; set; }

        /// <summary>
        /// Gets or sets the headers that should be assiociated with this request.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        public ClientWebSocketOptions()
        {
            Headers = new Dictionary<string, string>();
            Cookies = new CookieContainer();
            KeepAliveInterval = TimeSpan.FromSeconds(30);
            Credentials = new CredentialCache();
        }

        //public void AddSubProtocol(string subProtocol)
        //{
        //    throw new NotImplementedException();
        //}

        //public void SetBuffer(int receiveBufferSize, int sendBufferSize, ArraySegment<byte> buffer)
        //{
        //    throw new NotImplementedException();
        //}

        //public void SetBuffer(int receiveBufferSize, int sendBufferSize)
        //{
        //    throw new NotImplementedException();
        //}

    }
}
