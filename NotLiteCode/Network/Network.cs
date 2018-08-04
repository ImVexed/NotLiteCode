using NotLiteCode.Compression;
using NotLiteCode.Cryptography;
using NotLiteCode.Misc;
using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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

    private byte[] NextBufferLength = new byte[4];
    private bool Stopping = false;

    public readonly Socket BaseSocket;
    public readonly int BacklogLength;
    public readonly int ListenPort;

    public EncryptorOptions EncryptorOptions;
    public CompressorOptions CompressorOptions;
    public Encryptor Encryptor;

    /// <summary>
    /// Creates a new NLC Socket with default Socket, Compressor, & Encryptor options
    /// </summary>
    public NLCSocket() : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), new EncryptorOptions(), new CompressorOptions())
    { }

    /// <summary>
    /// Creates a new NLC Socket with custom Encryptor & Compressor options
    /// </summary>
    /// <param name="EncryptorOptions">Encryptor options for the Socket to use</param>
    /// <param name="CompressorOptions">Compressor options for the Socket to use</param>
    public NLCSocket(EncryptorOptions EncryptorOptions, CompressorOptions CompressorOptions) : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), EncryptorOptions, CompressorOptions)
    { }

    /// <summary>
    /// Creates a new NLC Socket with custom Encryptor, Compressor, & Socket options
    /// </summary>
    /// <param name="Socket">Underlying socket options</param>
    /// <param name="EncryptionOptions">Encryptor options for the Socket to use</param>
    /// <param name="CompressorOptions">Compressor options for the Socket to use</param>
    public NLCSocket(Socket Socket, EncryptorOptions EncryptorOptions, CompressorOptions CompressorOptions)
    {
      BaseSocket = Socket;
      this.EncryptorOptions = EncryptorOptions;
      this.CompressorOptions = CompressorOptions;
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
    public Task<int> Send(byte[] Buffer, int Offset, int Size, SocketFlags Flags, out SocketError SocketError)
    {
      lock (BaseSocket)
      {
        OnNetworkDataSent?.Invoke(this, new OnNetworkDataSentEventArgs(Size));
        return Task.FromResult(BaseSocket.Send(Buffer, Offset, Size, Flags, out SocketError));
      }
    }

    /// <summary>
    /// Reads raw data from the underlying socket
    /// </summary>
    public Task<int> Receive(byte[] Buffer, int Offset, int Size, SocketFlags Flags, out SocketError SocketError)
    {
      lock (BaseSocket)
      {
        var Length = BaseSocket.Receive(Buffer, Offset, Size, Flags, out SocketError);
        OnNetworkDataReceived?.Invoke(this, new OnNetworkDataReceivedEventArgs(Length));
        return Task.FromResult(Length);
      }
    }

    /// <summary>
    /// Begin to listen for incoming connections
    /// </summary>
    /// <param name="ListenPort">Network port to listen on</param>
    /// <param name="BacklogLength">Maximum backlog length</param>
    public void Listen(int ListenPort = 1337, int BacklogLength = 5)
    {
      BaseSocket.Bind(new IPEndPoint(IPAddress.Any, ListenPort));
      BaseSocket.Listen(BacklogLength);
      BaseSocket.BeginAccept(AcceptCallback, null);
    }

    /// <summary>
    /// Connect to another socket
    /// </summary>
    /// <param name="Address">Remote socket address</param>
    /// <param name="Port">Remote socket port</param>
    public Task Connect(string Address = "localhost", int Port = 1337)
    {
      return Task.Run(() => BaseSocket.Connect(Address, Port));
    }

    /// <summary>
    /// Accept new clients loop
    /// </summary>
    private void AcceptCallback(IAsyncResult iAR)
    {
      if (Stopping)
        return;

      var ConnectingClient = BaseSocket.EndAccept(iAR);

      OnNetworkClientConnected?.Invoke(this, new OnNetworkClientConnectedEventArgs(new NLCSocket(ConnectingClient, EncryptorOptions, CompressorOptions)));

      BaseSocket.BeginAccept(AcceptCallback, null);
    }

    /// <summary>
    /// Begin accepting messages
    /// </summary>
    public void BeginAcceptMessages()
    {
      BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
    }

    /// <summary>
    /// Main message received loop
    /// </summary>
    private async void MessageRetrieveCallback(IAsyncResult AsyncResult)
    {
      if (Stopping)
        return;

      // Check the message state to see if we've been disconnected
      if (BaseSocket.EndReceive(AsyncResult, out var ErrorCode) == 0 || ErrorCode != SocketError.Success)
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
      if ((BytesReceived = await this.Receive(Buffer, 0, BufferLength, SocketFlags.None, out ErrorCode)) != BufferLength || ErrorCode != SocketError.Success)
      {
        OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {BufferLength} got {BytesReceived} with exception {ErrorCode}")));
        BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
      }

      var DecompressedBuffer = this.CompressorOptions.DisableCompression ? Buffer : await Compressor.Decompress(Buffer);

      // Decrypt unless explicitly disabled
      var DecryptedBuffer = this.EncryptorOptions.DisableEncryption ? DecompressedBuffer : await this.Encryptor.Decrypt(DecompressedBuffer);

      // Deserialize the decrypted message into a raw object array
      var DeserializedEvent = await Serializer.Deserialize(DecryptedBuffer);

      // Parse the raw object array into a formatted network event
      if (!NetworkEvent.TryParse(DeserializedEvent, out var Event))
      {
        OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception("Failed to parse network event!")));
        BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
      }

      // Notify that we've received a network event
      OnNetworkMessageReceived?.Invoke(this, new OnNetworkMessageReceivedEventArgs(Event));

      // Loop
      BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
    }

    /// <summary>
    /// Synchronously send a network event
    /// </summary>
    /// <param name="Event">Event to send</param>
    /// <param name="Encrypt">Toggle encryption, this should only be used during the handshaking process</param>
    public async Task<bool> BlockingSend(NetworkEvent Event, bool Encrypt = true)
    {
      var Buffer = await Serializer.Serialize(Event.Package());

      if (!this.EncryptorOptions.DisableEncryption && Encrypt)
        Buffer = await Encryptor.Encrypt(Buffer);
      if (!this.CompressorOptions.DisableCompression)
        Buffer = await Compressor.Compress(Buffer);

      int BytesSent;
      if ((BytesSent = await this.Send(BitConverter.GetBytes(Buffer.Length), 0, 4, SocketFlags.None, out var ErrorCode)) != 4 || ErrorCode != SocketError.Success)
      {
        OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data sent to Client! Expected {4} sent {BytesSent} with exception {ErrorCode}")));
        return false;
      }

      if ((BytesSent = await this.Send(Buffer, 0, Buffer.Length, SocketFlags.None, out ErrorCode)) != Buffer.Length || ErrorCode != SocketError.Success)
      {
        OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data sent to Client! Expected {Buffer.Length} sent {BytesSent} with exception {ErrorCode}")));
        return false;
      }

      return true;
    }

    private const int P384_POINT_BYTELENGTH = 48;

    /// <summary>
    /// Synchronously receive a network event, note that this can be interfeared with if MessageRetrieveCallback is listening for messages (in server mode)
    /// </summary>
    /// <param name="Decrypt">Toggle encryption, this should only be used during the handshaking process</param>
    public async Task<NetworkEvent> BlockingReceive(bool Decrypt = true)
    {
      byte[] NewBufferLength = new byte[4];

      int BytesReceived;
      if ((BytesReceived = await this.Receive(NewBufferLength, 0, 4, SocketFlags.None, out var ErrorCode)) != 4 || ErrorCode != SocketError.Success)
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
        BytesReceiving = await this.Receive(Buffer, 0, BufferLength, SocketFlags.None, out ErrorCode);
        if (ErrorCode != SocketError.Success)
        {
          OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {BufferLength} got {BytesReceived} with exception {ErrorCode}")));
          return default(NetworkEvent);
        }
        else
          BytesReceived += BytesReceiving;
      }

      if (!this.CompressorOptions.DisableCompression)
        Buffer = await Compressor.Decompress(Buffer);
      if (!this.EncryptorOptions.DisableEncryption && Decrypt)
        Buffer = await Encryptor.Decrypt(Buffer);

      var DeserializedEvent = await Serializer.Deserialize(Buffer);

      if (!NetworkEvent.TryParse(DeserializedEvent, out var Event))
      {
        OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception("Failed to parse network event!")));
        return default(NetworkEvent);
      }

      return Event;
    }

    /// <summary>
    /// Try to initiate a handshake
    /// </summary>
    public async Task<bool> TrySendHandshake()
    {
#if NETCOREAPP2_1
      var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);

      var ServerPublicEvent = new NetworkEvent(NetworkHeader.HEADER_HANDSHAKE, null, ecdh.PublicKey.ToByteArray());

      if (!await BlockingSend(ServerPublicEvent, false))
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      NetworkEvent ClientPublicEvent;

      if ((ClientPublicEvent = await BlockingReceive(false)) == default(NetworkEvent))
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      if (ClientPublicEvent.Header != NetworkHeader.HEADER_HANDSHAKE)
      {
        this.Encryptor = default(Encryptor);
        return false;
      }
      
      var pubkey = ClientPublicEvent.Data as byte[];

      var dummyecdh = ECDiffieHellman.Create(new ECParameters()
      {
        Curve = ECCurve.NamedCurves.nistP384,
        Q = new ECPoint()
        {
          X = await pubkey.Slice(8, P384_POINT_BYTELENGTH),
          Y = await pubkey.Slice(8 + P384_POINT_BYTELENGTH, P384_POINT_BYTELENGTH)
        }
      });

      this.Encryptor = new Encryptor(ecdh.DeriveKeyMaterial(dummyecdh.PublicKey), EncryptorOptions);
      return true;
      
#else
      CngKey ECDH = CngKey.Create(CngAlgorithm.ECDiffieHellmanP256);
      byte[] ServerPublicKey = ECDH.Export(CngKeyBlobFormat.EccPublicBlob);

      var ServerPublicEvent = new NetworkEvent(NetworkHeader.HEADER_HANDSHAKE, null, ServerPublicKey);

      if (!await BlockingSend(ServerPublicEvent, false))
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      NetworkEvent ClientPublicEvent;

      if ((ClientPublicEvent = await BlockingReceive(false)) == default(NetworkEvent))
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      if (ClientPublicEvent.Header != NetworkHeader.HEADER_HANDSHAKE)
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      byte[] ClientKey = ClientPublicEvent.Data as byte[];

      using (var ECDHDerive = new ECDiffieHellmanCng(ECDH))
      using (CngKey ClientPublicKey = CngKey.Import(ClientKey, CngKeyBlobFormat.EccPublicBlob))
      {
        this.Encryptor = new Encryptor(ECDHDerive.DeriveKeyMaterial(ClientPublicKey), EncryptorOptions);
        return true;
      }
#endif
    }

    /// <summary>
    /// Try to receive a handshake
    /// </summary>
    public async Task<bool> TryReceiveHandshake()
    {
#if NETCOREAPP2_1
      var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);

      NetworkEvent ClientPublicEvent;

      if ((ClientPublicEvent = await BlockingReceive(false)) == default(NetworkEvent))
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      if (ClientPublicEvent.Header != NetworkHeader.HEADER_HANDSHAKE)
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      var pubkey = ClientPublicEvent.Data as byte[];

      var dummyecdh = ECDiffieHellman.Create(new ECParameters()
      {
        Curve = ECCurve.NamedCurves.nistP384,
        Q = new ECPoint()
        {
          X = await pubkey.Slice(8, P384_POINT_BYTELENGTH),
          Y = await pubkey.Slice(8 + P384_POINT_BYTELENGTH, P384_POINT_BYTELENGTH)
        }
      });

      this.Encryptor = new Encryptor(ecdh.DeriveKeyMaterial(dummyecdh.PublicKey), EncryptorOptions);

      var ServerPublicEvent = new NetworkEvent(NetworkHeader.HEADER_HANDSHAKE, null, ecdh.PublicKey.ToByteArray());

      if (!await BlockingSend(ServerPublicEvent, false))
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      return true;
#else
      CngKey ECDH = CngKey.Create(CngAlgorithm.ECDiffieHellmanP256);
      byte[] ClientPublicKey = ECDH.Export(CngKeyBlobFormat.EccPublicBlob);

      NetworkEvent ClientPublicEvent;

      if ((ClientPublicEvent = await BlockingReceive(false)) == default(NetworkEvent))
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      if (ClientPublicEvent.Header != NetworkHeader.HEADER_HANDSHAKE)
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      byte[] ServerKey = ClientPublicEvent.Data as byte[];

      using (var ECDHDerive = new ECDiffieHellmanCng(ECDH))
      using (CngKey ServerPublicKey = CngKey.Import(ServerKey, CngKeyBlobFormat.EccPublicBlob))
        this.Encryptor = new Encryptor(ECDHDerive.DeriveKeyMaterial(ServerPublicKey), EncryptorOptions);

      var ServerPublicEvent = new NetworkEvent(NetworkHeader.HEADER_HANDSHAKE, null, ClientPublicKey);

      if (!await BlockingSend(ServerPublicEvent, false))
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      return true;
#endif
    }
  }
}