using NotLiteCode.Serializer;
using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

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

        private readonly SemaphoreSlim BaseSocketReadSem = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim BaseSocketWriteSem = new SemaphoreSlim(1, 1);
        private byte[] NextBufferLength = new byte[4];
        private bool Stopping = false;

        public readonly Socket BaseSocket;
        public readonly int BacklogLength;
        public readonly int ListenPort;

        private readonly ISerializationProdiver SerializationProdiver;
        private readonly X509Certificate2 ServerCertificate;
        private readonly bool AllowInsecureCerts;
        private SslStream SSLStream;

        public readonly bool UseSSL;
        public readonly bool UseCompression;
        public bool IsListening = false;

        /// <summary>
        /// Continue to BeginAccept messages
        /// </summary>
        public bool ContinueSubscribing = true;

        /// <param name="UseSSL">Option to enable encryption of data via SSL</param>
        /// <param name="AllowInsecureCerts">Don't attempt to validate the server's certificate client side</param>
        /// <param name="ServerCertificate">Certificate to serve to the client while establishing an SSL connection</param>
        public NLCSocket(bool UseSSL = false, bool AllowInsecureCerts = false, X509Certificate2 ServerCertificate = null)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), new DefaultSerializationProvider(), UseSSL, AllowInsecureCerts, ServerCertificate)
        { }

        /// <param name="Socket">Underlying socket to use</param>
        /// <param name="UseSSL">Option to enable encryption of data via SSL</param>
        /// <param name="AllowInsecureCerts">Don't attempt to validate the server's certificate client side</param>
        /// <param name="ServerCertificate">Certificate to serve to the client while establishing an SSL connection</param>
        public NLCSocket(Socket Socket, bool UseSSL = false, bool AllowInsecureCerts = false, X509Certificate2 ServerCertificate = null)
            : this(Socket, new DefaultSerializationProvider(), UseSSL, AllowInsecureCerts, ServerCertificate)
        { }

        /// <param name="Serializer">Serializer to serialize messages with</param>
        /// <param name="UseSSL">Option to enable encryption of data via SSL</param>
        /// <param name="AllowInsecureCerts">Don't attempt to validate the server's certificate client side</param>
        /// <param name="ServerCertificate">Certificate to serve to the client while establishing an SSL connection</param>
        public NLCSocket(ISerializationProdiver Serializer, bool UseSSL = false, bool AllowInsecureCerts = false, X509Certificate2 ServerCertificate = null)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), Serializer, UseSSL, AllowInsecureCerts, ServerCertificate)
        { }

        /// <param name="Socket">Underlying socket to use</param>
        /// <param name="Serializer">Serializer to serialize messages with</param>
        /// <param name="UseSSL">Option to enable encryption of data via SSL</param>
        /// <param name="AllowInsecureCerts">Don't attempt to validate the server's certificate client side</param>
        /// <param name="ServerCertificate">Certificate to serve to the client while establishing an SSL connection</param>
        public NLCSocket(Socket Socket, ISerializationProdiver Serializer, bool UseSSL = false, bool AllowInsecureCerts = false, X509Certificate2 ServerCertificate = null)
        {
            BaseSocket = Socket;

            this.UseSSL = UseSSL;
            this.ServerCertificate = ServerCertificate;
            this.AllowInsecureCerts = AllowInsecureCerts;
            this.SerializationProdiver = Serializer;

            if (BaseSocket.Connected)
            {
                if (UseSSL)
                {
                    SetupSSL();

                    SSLStream.AuthenticateAsServer(ServerCertificate, clientCertificateRequired: false, checkCertificateRevocation: true, enabledSslProtocols: SslProtocols.Tls12);
                }
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

            OnNetworkDataSent?.Start(this, new OnNetworkDataSentEventArgs(Length));

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

            OnNetworkDataReceived?.Start(this, new OnNetworkDataReceivedEventArgs(Length));

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

            if (UseSSL)
            {
                SetupSSL();
                SSLStream.AuthenticateAsClient(Address, clientCertificates: null, enabledSslProtocols: SslProtocols.Tls12, checkCertificateRevocation: true);
            }
        }

        private void SetupSSL()
        {
            if (AllowInsecureCerts)
                SSLStream = new SslStream(new NetworkStream(BaseSocket), false, new RemoteCertificateValidationCallback((w, x, y, z) => true), null);
            else
                SSLStream = new SslStream(new NetworkStream(BaseSocket), false);
        }

        /// <summary>
        /// Accept new clients loop
        /// </summary>
        private void AcceptCallback(IAsyncResult iAR)
        {
            if (Stopping)
                return;

            var ConnectingClient = BaseSocket.EndAccept(iAR);

            OnNetworkClientConnected?.Start(this, new OnNetworkClientConnectedEventArgs(new NLCSocket(ConnectingClient, SerializationProdiver, UseSSL, AllowInsecureCerts, ServerCertificate)));

            BaseSocket.BeginAccept(AcceptCallback, null);
        }

        /// <summary>
        /// Begin accepting messages
        /// </summary>
        public void BeginAcceptMessages()
        {
            this.IsListening = true;

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
                OnNetworkClientDisconnected?.Start(this, new OnNetworkClientDisconnectedEventArgs(BaseSocket?.RemoteEndPoint));

                this.Close();
                this.IsListening = false;

                return;
            }

            // Take our initial first 4 bytes we've received so we know how large the actual message is
            var BufferLength = BitConverter.ToInt32(NextBufferLength, 0);
            NextBufferLength = new byte[4];

            var Buffer = new byte[BufferLength];

            var BytesReceived = 0;

            while (BytesReceived < BufferLength)
            {
                var BytesReceiving = this.Receive(Buffer, BytesReceived, BufferLength - BytesReceived, SocketFlags.None, out var ErrorCode);
                if (ErrorCode != SocketError.Success)
                {
                    OnNetworkExceptionOccurred?.Start(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {BufferLength} got {BytesReceived} with exception {ErrorCode}")));
                    BeginAcceptMessages();
                    return;
                }
                else
                    BytesReceived += BytesReceiving;
            }

            // Deserialize the decrypted message into a raw object array
            NetworkEvent DeserializedEvent;

            try
            {
                DeserializedEvent = SerializationProdiver.Deserialize<NetworkEvent>(Buffer);
            }
            catch
            {
                OnNetworkExceptionOccurred?.Start(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Corrupted data received!")));
                BeginAcceptMessages();
                return;
            }

            // Notify that we've received a network event
            OnNetworkMessageReceived?.Start(this, new OnNetworkMessageReceivedEventArgs(DeserializedEvent));

            // Loop
            if (ContinueSubscribing)
                BeginAcceptMessages();
        }

        /// <summary>
        /// Synchronously send a network event
        /// </summary>
        /// <param name="Event">Event to send</param>
        public async Task<bool> BlockingSend(NetworkEvent Event)
        {
            await BaseSocketWriteSem.WaitAsync();

            var Buffer = SerializationProdiver.Serialize(Event);

            int BytesSent;

            if ((BytesSent = this.Send(BitConverter.GetBytes(Buffer.Length), 0, 4, SocketFlags.None, out var ErrorCode)) != 4 || ErrorCode != SocketError.Success)
            {
                OnNetworkExceptionOccurred?.Start(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data sent to Client! Expected {Buffer.Length} sent {BytesSent} with exception {ErrorCode}")));
                return false;
            }

            if ((BytesSent = this.Send(Buffer, 0, Buffer.Length, SocketFlags.None, out ErrorCode)) != Buffer.Length || ErrorCode != SocketError.Success)
            {
                OnNetworkExceptionOccurred?.Start(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data sent to Client! Expected {Buffer.Length} sent {BytesSent} with exception {ErrorCode}")));
                return false;
            }

            BaseSocketWriteSem.Release();

            return true;
        }

        /// <summary>
        /// Synchronously receive a network event, note that this can interfeared with MessageRetrieveCallback if the base socket is already blocking
        /// </summary>
        public async Task<NetworkEvent> BlockingReceive()
        {
            await BaseSocketReadSem.WaitAsync();

            byte[] NewBufferLength = new byte[4];

            int BytesReceived;
            if ((BytesReceived = this.Receive(NewBufferLength, 0, 4, SocketFlags.None, out var ErrorCode)) != 4 || ErrorCode != SocketError.Success)
            {
                OnNetworkExceptionOccurred?.Start(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {4} got {BytesReceived} with exception {ErrorCode}")));
                return default(NetworkEvent);
            }

            var BufferLength = BitConverter.ToInt32(NewBufferLength, 0);
            var Buffer = new byte[BufferLength];

            BytesReceived = 0;
            while (BytesReceived < BufferLength)
            {
                var BytesReceiving = this.Receive(Buffer, BytesReceived, BufferLength - BytesReceived, SocketFlags.None, out ErrorCode);
                if (ErrorCode != SocketError.Success)
                {
                    OnNetworkExceptionOccurred?.Start(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {BufferLength} got {BytesReceived} with exception {ErrorCode}")));
                    return default(NetworkEvent);
                }
                else
                    BytesReceived += BytesReceiving;
            }

            var Event = SerializationProdiver.Deserialize<NetworkEvent>(Buffer);

            BaseSocketReadSem.Release();

            return Event;
        }
    }
}