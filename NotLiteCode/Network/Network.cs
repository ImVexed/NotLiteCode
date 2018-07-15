using NotLiteCode.Compression;
using NotLiteCode.Cryptography;
using NotLiteCode.Misc;
using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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

    private byte[] NextBufferLength = new byte[4];
    private bool Stopping = false;

    public readonly Socket BaseSocket;
    public readonly int BacklogLength;
    public readonly int ListenPort;

    public EncryptionOptions EncryptionOptions;
    public CompressorOptions CompressorOptions;
    public Encryptor Encryptor;

    public NLCSocket() : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), new EncryptionOptions(), new CompressorOptions())
    { }

    public NLCSocket(EncryptionOptions EncryptionOptions, CompressorOptions CompressorOptions) : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), EncryptionOptions, CompressorOptions)
    { }

    public NLCSocket(Socket Socket, EncryptionOptions EncryptionOptions, CompressorOptions CompressorOptions)
    {
      BaseSocket = Socket;
      this.EncryptionOptions = EncryptionOptions;
      this.CompressorOptions = CompressorOptions;
    }

    public void Close()
    {
      Stopping = true;
      BaseSocket.Close();
    }

    public Task<int> Send(byte[] Buffer, int Offset, int Size, SocketFlags Flags, out SocketError SocketError)
    {
      lock (BaseSocket)
      {
        OnNetworkDataSent?.Invoke(this, new OnNetworkDataSentEventArgs(Size));
        return Task.FromResult(BaseSocket.Send(Buffer, Offset, Size, Flags, out SocketError));
      }
    }

    public Task<int> Receive(byte[] Buffer, int Offset, int Size, SocketFlags Flags, out SocketError SocketError)
    {
      lock (BaseSocket)
      {
        var Length = BaseSocket.Receive(Buffer, Offset, Size, Flags, out SocketError);
        OnNetworkDataReceived?.Invoke(this, new OnNetworkDataReceivedEventArgs(Length));
        return Task.FromResult(Length);
      }
    }

    public void Listen(int ListenPort = 1337, int BacklogLength = 5)
    {
      BaseSocket.Bind(new IPEndPoint(IPAddress.Any, ListenPort));
      BaseSocket.Listen(BacklogLength);
    }

    public Task Connect(string Address = "localhost", int Port = 1337)
    {
      return Task.Run(() => BaseSocket.Connect(Address, Port));
    }

    public void BeginAcceptClients()
    {
      BaseSocket.BeginAccept(AcceptCallback, null);
    }

    private void AcceptCallback(IAsyncResult iAR)
    {
      if (Stopping)
        return;

      var ConnectingClient = BaseSocket.EndAccept(iAR);

      OnNetworkClientConnected?.Invoke(this, new OnNetworkClientConnectedEventArgs(new NLCSocket(ConnectingClient, EncryptionOptions, CompressorOptions)));

      BaseSocket.BeginAccept(AcceptCallback, null);
    }

    public void BeginAcceptMessages()
    {
      BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
    }

    private async void MessageRetrieveCallback(IAsyncResult AsyncResult)
    {
      if (Stopping)
        return;

      if (BaseSocket.EndReceive(AsyncResult, out var ErrorCode) == 0 || ErrorCode != SocketError.Success)
      {
        OnNetworkClientDisconnected?.Invoke(this, new OnNetworkClientDisconnectedEventArgs(BaseSocket?.RemoteEndPoint));
        this.Close();
        return;
      }

      var BufferLength = BitConverter.ToInt32(NextBufferLength, 0);
      NextBufferLength = new byte[4];

      var Buffer = new byte[BufferLength];

      int BytesReceived;
      if ((BytesReceived = await this.Receive(Buffer, 0, BufferLength, SocketFlags.None, out ErrorCode)) != BufferLength || ErrorCode != SocketError.Success)
      {
        OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {BufferLength} got {BytesReceived} with exception {ErrorCode}")));
        BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
      }

      var DecryptedBuffer = this.EncryptionOptions.DisableEncryption ? Buffer : await this.Encryptor.Decrypt(Buffer);

      var DeserializedEvent = await Serializer.Deserialize(DecryptedBuffer);

      if (!NetworkEvent.TryParse(DeserializedEvent, out var Event))
      {
        OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception("Failed to parse network event!")));
        BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
      }

      OnNetworkMessageReceived?.Invoke(this, new OnNetworkMessageReceivedEventArgs(Event));

      BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
    }

    public async Task<bool> BlockingSend(NetworkEvent Event, bool Encrypt = true)
    {
      var Buffer = await Serializer.Serialize(Event.Package());

      if (!this.EncryptionOptions.DisableEncryption && Encrypt)
        Buffer = await Encryptor.Encrypt(Buffer);
      else if (!this.CompressorOptions.DisableCompression)
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

      if (!this.EncryptionOptions.DisableEncryption && Decrypt)
        Buffer = await Encryptor.Decrypt(Buffer);
      else if (!this.CompressorOptions.DisableCompression)
        Buffer = await Compressor.Decompress(Buffer);

      var DeserializedEvent = await Serializer.Deserialize(Buffer);

      if (!NetworkEvent.TryParse(DeserializedEvent, out var Event))
      {
        OnNetworkExceptionOccurred?.Invoke(this, new OnNetworkExceptionOccurredEventArgs(new Exception("Failed to parse network event!")));
        return default(NetworkEvent);
      }

      return Event;
    }

    public async Task<bool> TrySendHandshake()
    {
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
        this.Encryptor = new Encryptor(ECDHDerive.DeriveKeyMaterial(ClientPublicKey), EncryptionOptions, CompressorOptions);
        return true;
      }
    }

    public async Task<bool> TryReceiveHandshake()
    {
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
        this.Encryptor = new Encryptor(ECDHDerive.DeriveKeyMaterial(ServerPublicKey), EncryptionOptions, CompressorOptions);

      var ServerPublicEvent = new NetworkEvent(NetworkHeader.HEADER_HANDSHAKE, null, ClientPublicKey);

      if (!await BlockingSend(ServerPublicEvent, false))
      {
        this.Encryptor = default(Encryptor);
        return false;
      }

      return true;
    }
  }
}