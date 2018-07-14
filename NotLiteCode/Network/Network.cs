using NotLiteCode.Encryption;
using System;
using System.Net;
using System.Net.Sockets;

namespace NotLiteCode.Network
{
  public enum NetworkHeader
  {
    NONE,
    HEADER_CALL,
    HEADER_RETURN,
    HEADER_HANDSHAKE,
    HEADER_MOVE,
    HEADER_ERROR
  }

  public class NLCSocket
  {
    public event EventHandler<OnMessageReceivedEventArgs> OnMessageReceived;

    public event EventHandler<OnExceptionOccurredEventArgs> OnExceptionOccurred;

    public event EventHandler<OnClientConnectedEventArgs> OnClientConnected;

    public event EventHandler<OnDataReceivedEventArgs> OnDataReceived;

    public event EventHandler<OnDataSentEventArgs> OnDataSent;

    public event EventHandler OnClientDisconnected;

    private byte[] NextBufferLength = new byte[4];
    public readonly Socket BaseSocket;
    private bool Stopping = false;

    public readonly int BacklogLength;
    public readonly int ListenPort;

    public Encryptor Encryptor;

    public NLCSocket(AddressFamily AddressFamily = AddressFamily.InterNetwork,
                     SocketType SocketType = SocketType.Stream,
                     ProtocolType ProtocolType = ProtocolType.Tcp)
    {
      BaseSocket = new Socket(AddressFamily, SocketType, ProtocolType);
    }

    public NLCSocket(Socket Socket)
    {
      BaseSocket = Socket;
    }

    public void Close()
    {
      Stopping = true;
      BaseSocket.Close();
    }

    public int Send(byte[] Buffer, int Offset, int Size, SocketFlags Flags, out SocketError SocketError)
    {
      OnDataSent?.Invoke(this, new OnDataSentEventArgs(Size));
      return BaseSocket.Send(Buffer, Offset, Size, Flags, out SocketError);
    }

    public int Receive(byte[] Buffer, int Offset, int Size, SocketFlags Flags, out SocketError SocketError)
    {
      var Length = BaseSocket.Receive(Buffer, Offset, Size, Flags, out SocketError);
      OnDataReceived?.Invoke(this, new OnDataReceivedEventArgs(Length));
      return Length;
    }

    public void Listen(int ListenPort = 1337, int BacklogLength = 5)
    {
      BaseSocket.Bind(new IPEndPoint(IPAddress.Any, ListenPort));
      BaseSocket.Listen(BacklogLength);
    }

    public void Connect(string Address = "localhost", int Port = 1337)
    {
      BaseSocket.Connect(Address, Port);
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

      OnClientConnected?.Invoke(this, new OnClientConnectedEventArgs(new NLCSocket(ConnectingClient)));

      BaseSocket.BeginAccept(AcceptCallback, null);
    }

    public void BeginAcceptMessages(Encryptor Encryptor)
    {
      this.Encryptor = Encryptor;

      BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
    }

    private void MessageRetrieveCallback(IAsyncResult AsyncResult)
    {
      if (Stopping)
        return;

      if (BaseSocket.EndReceive(AsyncResult, out var ErrorCode) == 0 || ErrorCode != SocketError.Success)
      {
        OnClientDisconnected?.Invoke(this, null);
        this.Close();
        return;
      }

      var BufferLength = BitConverter.ToInt32(NextBufferLength, 0);
      NextBufferLength = new byte[4];

      var Buffer = new byte[BufferLength];

      int BytesReceived;
      if ((BytesReceived = this.Receive(Buffer, 0, BufferLength, SocketFlags.None, out ErrorCode)) != BufferLength || ErrorCode != SocketError.Success)
      {
        OnExceptionOccurred?.Invoke(this, new OnExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {BufferLength} got {BytesReceived} with exception {ErrorCode}")));
        BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
      }

      var DecryptedBuffer = this.Encryptor.AES_Decrypt(Buffer);

      var DeserializedEvent = Serializer.Deserialize(DecryptedBuffer);

      if (!NetworkEvent.TryParse(DeserializedEvent, out var Event))
      {
        OnExceptionOccurred?.Invoke(this, new OnExceptionOccurredEventArgs(new Exception("Failed to parse network event!")));
        BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
      }

      OnMessageReceived?.Invoke(this, new OnMessageReceivedEventArgs(Event));

      BaseSocket.BeginReceive(NextBufferLength, 0, 4, SocketFlags.None, MessageRetrieveCallback, null);
    }

    public bool TryBlockingSend(NetworkEvent Event, bool Encrypt = true)
    {
      var Buffer = Serializer.Serialize(Event.Package());

      if (Encrypt)
        Buffer = Encryptor.AES_Encrypt(Buffer);
      else
        Buffer = Compressor.Compress(Buffer);

      int BytesSent;
      if ((BytesSent = this.Send(BitConverter.GetBytes(Buffer.Length), 0, 4, SocketFlags.None, out var ErrorCode)) != 4 || ErrorCode != SocketError.Success)
      {
        OnExceptionOccurred?.Invoke(this, new OnExceptionOccurredEventArgs(new Exception($"Invalid ammount of data sent to Client! Expected {4} sent {BytesSent} with exception {ErrorCode}")));
        return false;
      }

      if ((BytesSent = this.Send(Buffer, 0, Buffer.Length, SocketFlags.None, out ErrorCode)) != Buffer.Length || ErrorCode != SocketError.Success)
      {
        OnExceptionOccurred?.Invoke(this, new OnExceptionOccurredEventArgs(new Exception($"Invalid ammount of data sent to Client! Expected {Buffer.Length} sent {BytesSent} with exception {ErrorCode}")));
        return false;
      }

      return true;
    }

    public bool TryBlockingReceive(out NetworkEvent Event, bool Decrypt = true)
    {
      byte[] NewBufferLength = new byte[4];

      int BytesReceived;
      if ((BytesReceived = this.Receive(NewBufferLength, 0, 4, SocketFlags.None, out var ErrorCode)) != 4 || ErrorCode != SocketError.Success)
      {
        OnExceptionOccurred?.Invoke(this, new OnExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {4} got {BytesReceived} with exception {ErrorCode}")));
        Event = default(NetworkEvent);
        return false;
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
          OnExceptionOccurred?.Invoke(this, new OnExceptionOccurredEventArgs(new Exception($"Invalid ammount of data received from Client! Expected {BufferLength} got {BytesReceived} with exception {ErrorCode}")));
          Event = default(NetworkEvent);
          return false;
        }
        else
          BytesReceived += BytesReceiving;
      }
      if (Decrypt)
        Buffer = Encryptor.AES_Decrypt(Buffer);
      else
        Buffer = Compressor.Decompress(Buffer);

      var DeserializedEvent = Serializer.Deserialize(Buffer);

      if (!NetworkEvent.TryParse(DeserializedEvent, out Event))
      {
        OnExceptionOccurred?.Invoke(this, new OnExceptionOccurredEventArgs(new Exception("Failed to parse network event!")));
        Event = default(NetworkEvent);
        return false;
      }

      return true;
    }
  }
}