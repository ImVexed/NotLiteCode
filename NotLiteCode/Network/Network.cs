using NotLiteCode.Misc;
using System;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace NotLiteCode.Network
{
    public enum NetworkHeader
    {
        NONE,
        HEADER_HANDSHAKE,
        HEADER_CALL,
        HEADER_MOVE,
        HEADER_RETURN,
        HEADER_ERROR
    }

    public class NLCSocket
    {
        public event EventHandler<OnNetworkClientDisconnectedEventArgs> OnNetworkClientDisconnected;

        public event EventHandler<OnNetworkExceptionOccurredEventArgs> OnNetworkExceptionOccurred;

        public event EventHandler<OnNetworkMessageReceivedEventArgs> OnNetworkMessageReceived;

        public event EventHandler<OnNetworkClientConnectedEventArgs> OnNetworkClientConnected;

        public event EventHandler<OnNetworkDataReceivedEventArgs> OnNetworkDataReceived;

        public event EventHandler<OnNetworkDataSentEventArgs> OnNetworkDataSent;

        private object BaseSocketReadLock = new object();
        private object BaseSocketWriteLock = new object();
        private byte[] NextBufferLength = new byte[4];
        private bool Stopping = false;

        public readonly Socket BaseSocket;
        public readonly int BacklogLength;
        public readonly int ListenPort;

        private X509Certificate2 ServerCertificate;
        private bool AllowInsecureCerts;
        private SslStream SSLStream;

        public readonly bool UseSSL;
        public readonly bool UseCompression;

        /// <summary>
        /// Continue to BeginAccept messages
        /// </summary>
        public bool ContinueSubscribing = true;

        /// <summary>
        /// Creates a new NLC Socket with default Socket, Compressor, & Encryptor options
        /// </summary>
        public NLCSocket() : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), false, false, null)
        { }

        /// <summary>
        /// Creates a new NLC Socket with custom Encryptor & Compressor options
        /// </summary>
        /// <param name="UseSSL">Option to enable encryption of data via SSL, ONLY USE THIS IF YOU ARE A CLIENT, if you are a server, be sure to provide the Server's Certificate</param>
        public NLCSocket(bool UseSSL) : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), UseSSL, false, null)
        { }

        /// <summary>
        /// Creates a new NLC Socket with custom Encryptor & Compressor options
        /// </summary>
        /// <param name="UseSSL">Option to enable encryption of data via SSL, ONLY USE THIS IF YOU ARE A CLIENT, if you are a server, be sure to provide the Server's Certificate</param>
        /// <param name="AllowInsecureCerts">Don't attempt to validate the server's certificate</param>
        public NLCSocket(bool UseSSL, bool AllowInsecureCerts) : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), UseSSL, AllowInsecureCerts, null)
        { }

        /// <summary>
        /// Creates a new NLC Socket with custom Encryptor & Compressor options
        /// </summary>
        /// <param name="UseSSL">Option to enable communication over SSL</param>
        /// <param name="ServerCertificate">Custom server certificate for the SSLStream</param>
        public NLCSocket(bool UseSSL, X509Certificate2 ServerCertificate) : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), UseSSL, false, ServerCertificate)
        { }

        /// <summary>
        /// Creates a new NLC Socket with custom Encryptor, Compressor, & Socket options
        /// </summary>
        /// <param name="Socket">Underlying socket options</param>
        /// <param name="UseSSL">Option to enable communication over SSL</param>
        /// <param name="UseCompression">Option to enable compression of data</param>
        /// <param name="AllowInsecureCerts">Don't attempt to validate the server's certificate</param>
        /// <param name="ServerCertificate">The certificate to use when accepting new clients via SSL</param>
        public NLCSocket(Socket Socket, bool UseSSL, bool AllowInsecureCerts, X509Certificate2 ServerCertificate)
        {
            BaseSocket = Socket;

            this.UseSSL = UseSSL;
            this.ServerCertificate = ServerCertificate;
            this.AllowInsecureCerts = AllowInsecureCerts;

            if (BaseSocket.Connected)
            {
                SetupSSL();

                if (UseSSL)
                    SSLStream.AuthenticateAsServer(ServerCertificate, clientCertificateRequired: false, checkCertificateRevocation: true, enabledSslProtocols: SslProtocols.Tls12);
            }
        }

        /// <summary>
        /// Close the socket
        /// </summary>
        public void Close()
        {
            Stopping = true;
            BaseSocket.Close();
        }

        /// <summary>
        /// Send raw data over the underlying socket
        /// </summary>
        public int Send(byte[] Buffer, int Offset, int Size, SocketFlags Flags, out SocketError SocketError)
        {
            int Length = Size;

            SocketError = SocketError.Success;

            try
            {
                if (UseSSL)
                    SSLStream.Write(Buffer, Offset, Size);
                else
                    Length = BaseSocket.Send(Buffer, Offset, Size, Flags, out SocketError);
            }
            catch
            {
                SocketError = SocketError.SocketError;
            }

            OnNetworkDataSent?.Invoke(this, new OnNetworkDataSentEventArgs(Length));

            return Length;
        }

        /// <summary>
        /// Reads raw data from the underlying socket
        /// </summary>
        public int Receive(byte[] Buffer, int Offset, int Size, SocketFlags Flags, out SocketError SocketError)
        {
            int Length = 0;

            SocketError = SocketError.Success;

            try
            {
                if (UseSSL)
                    Length = SSLStream.Read(Buffer, Offset, Size);
                else
                    Length = BaseSocket.Receive(Buffer, Offset, Size, Flags, out SocketError);
            }
            catch
            {
                SocketError = SocketError.SocketError;
            }

            OnNetworkDataReceived?.Invoke(this, new OnNetworkDataReceivedEventArgs(Length));

            return Length;
        }

        /// <summary>
        /// Begin to listen for incoming connections
        /// </summary>
        /// <param name="ListenPort">Network port to listen on</param>
        /// <param name="BacklogLength">Maximum backlog length</param>
        public void Listen(int ListenPort = 1337, int BacklogLength = 5)
        {
            // Don't re-bind the socket if it already has live connections
            if (!BaseSocket.IsBound)
            {
                BaseSocket.Bind(new IPEndPoint(IPAddress.Any, ListenPort));
                BaseSocket.Listen(BacklogLength);
            }

            BaseSocket.BeginAccept(AcceptCallback, null);
        }

        /// <summary>
        /// Connect to another socket
        /// </summary>
        /// <param name="Address">Remote socket address</param>
        /// <param name="Port">Remote socket port</param>
        public void Connect(string Address = "localhost", int Port = 1337)
        {
            BaseSocket.Connect(Address, Port);

            SetupSSL();

            if (UseSSL)
                SSLStream.AuthenticateAsClient(Address, clientCertificates: null, enabledSslProtocols: SslProtocols.Tls12, checkCertificateRevocation: true);
        }

        private void SetupSSL()
        {
            if (UseSSL)
            {
                if (AllowInsecureCerts)
                    SSLStream = new SslStream(new NetworkStream(BaseSocket), false, new RemoteCertificateValidationCallback((w, x, y, z) => true), null);
                else
                    SSLStream = new SslStream(new NetworkStream(BaseSocket), false);
            }
        }

        /// <summary>
        /// Accept new clients loop
        /// </summary>
        private void AcceptCallback(IAsyncResult iAR)
        {
            if (Stopping)
                return;

            var ConnectingClient = BaseSocket.EndAccept(iAR);

            OnNetworkClientConnected?.Invoke(this, new OnNetworkClientConnectedEventArgs(new NLCSocket(ConnectingClient, UseSSL, AllowInsecureCerts, ServerCertificate)));

            BaseSocket.BeginAccept(AcceptCallback, null);
        }

        /// <summary>
        /// Begin accepting messages
        /// </summary>
        public void BeginAcceptMessages()
        {
            if (UseSSL)
                SSLStream.BeginRead(NextBufferLength, 0, 4, MessageRetrieveCallback, null);
            else
                BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
        }

        /// <summary>
        /// Main message received loop
        /// </summary>
        private void MessageRetrieveCallback(IAsyncResult AsyncResult)
        {
            if (Stopping)
                return;

            bool StatusOK;

            try
            {
                if (UseSSL)
                    StatusOK = SSLStream.EndRead(AsyncResult) != 0;
                else
                    StatusOK = BaseSocket.EndReceive(AsyncResult, out var ErrorCode) != 0 && ErrorCode == SocketError.Success;
            }
            catch
            {
                StatusOK = false;
            }

            // Check the message state to see if we've been disconnected
            if (!StatusOK)
            {
                OnNetworkClientDisconnected?.Invoke(this, new OnNetworkClientDisconnectedEventArgs(BaseSocket?.RemoteEndPoint));
                this.Close();
                return;
            }

            // Take our initial first 4 bytes we've received so we know how large the actual message is
            var BufferLength = BitConverter.ToInt32(NextBufferLength, 0);
            NextBufferLength = new byte[4];

            var Buffer = new byte[BufferLength];

            // Keep receiving until we reach the specified message size
            int BytesReceived;
            if ((BytesReceived = this.Receive(Buffer, 0, BufferLength, SocketFlags.None, out var ReadCode)) != BufferLength || ReadCode != SocketError.Success)
            {
                OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {BufferLength} got {BytesReceived} with exception {ReadCode}")));
                BeginAcceptMessages();
                return;
            }

            // Deserialize the decrypted message into a raw object array
            var DeserializedEvent = Serializer.Deserialize(Buffer);

            // Parse the raw object array into a formatted network event
            if (!NetworkEvent.TryParse(DeserializedEvent, out var Event))
            {
                OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception("Failed to parse network event!")));
                BeginAcceptMessages();
                return;
            }

            // Notify that we've received a network event
            OnNetworkMessageReceived?.Invoke(this, new OnNetworkMessageReceivedEventArgs(Event));

            // Loop
            if (ContinueSubscribing)
                BeginAcceptMessages();
        }

        /// <summary>
        /// Synchronously send a network event
        /// </summary>
        /// <param name="Event">Event to send</param>
        /// <param name="Encrypt">Toggle encryption, this should only be used during the handshaking process</param>
        public bool BlockingSend(NetworkEvent Event, bool Encrypt = true)
        {
            lock (BaseSocketWriteLock)
            {
                var Buffer = Serializer.Serialize(Event.Package());

                int BytesSent;
                if ((BytesSent = this.Send(BitConverter.GetBytes(Buffer.Length), 0, 4, SocketFlags.None, out var ErrorCode)) != 4 || ErrorCode != SocketError.Success)
                {
                    OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data sent to Client! Expected 4 sent {BytesSent} with exception {ErrorCode}")));
                    return false;
                }

                if ((BytesSent = this.Send(Buffer, 0, Buffer.Length, SocketFlags.None, out ErrorCode)) != Buffer.Length || ErrorCode != SocketError.Success)
                {
                    OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data sent to Client! Expected {Buffer.Length} sent {BytesSent} with exception {ErrorCode}")));
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Synchronously receive a network event, note that this can interfeared with MessageRetrieveCallback if the base socket is already blocking
        /// </summary>
        public NetworkEvent BlockingReceive()
        {
            lock (BaseSocketReadLock)
            {
                byte[] NewBufferLength = new byte[4];

                int BytesReceived;
                if ((BytesReceived = this.Receive(NewBufferLength, 0, 4, SocketFlags.None, out var ErrorCode)) != 4 || ErrorCode != SocketError.Success)
                {
                    OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {4} got {BytesReceived} with exception {ErrorCode}")));
                    return default(NetworkEvent);
                }

                var BufferLength = BitConverter.ToInt32(NewBufferLength, 0);
                var Buffer = new byte[BufferLength];

                BytesReceived = 0;
                int BytesReceiving;

                while (BytesReceived < BufferLength)
                {
                    BytesReceiving = this.Receive(Buffer, 0, BufferLength, SocketFlags.None, out ErrorCode);
                    if (ErrorCode != SocketError.Success)
                    {
                        OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {BufferLength} got {BytesReceived} with exception {ErrorCode}")));
                        return default(NetworkEvent);
                    }
                    else
                        BytesReceived += BytesReceiving;
                }

                var DeserializedEvent = Serializer.Deserialize(Buffer);

                if (!NetworkEvent.TryParse(DeserializedEvent, out var Event))
                {
                    OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception("Failed to parse network event!")));
                    return default(NetworkEvent);
                }

                return Event;
            }
        }
    }
}