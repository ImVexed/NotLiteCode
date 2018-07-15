using NotLiteCode.Compression;
using NotLiteCode.Cryptography;
using NotLiteCode.Network;
using System;

namespace NotLiteCode
{
  public class Client
  {
    private NLCSocket ClientSocket;

    public Client() : this(new NLCSocket(new EncryptionOptions(), new CompressorOptions()))
    { }

    public Client(NLCSocket ClientSocket)
    {
      this.ClientSocket = ClientSocket;
    }

    public bool Connect(string ServerAddress, int ServerPort)
    {
      ClientSocket.Connect(ServerAddress, ServerPort);

      return ClientSocket.TryReceiveHandshake();
    }

    public void Stop()
    {
      ClientSocket.Close();
    }

    public T RemoteCall<T>(string identifier, params object[] param)
    {
      var Event = new NetworkEvents(NetworkHeader.HEADER_MOVE, identifier, param);

      if (!ClientSocket.TryBlockingSend(Event))
        throw new Exception("Failed to sent request to server!");

      if (!ClientSocket.TryBlockingReceive(out var ReturnEvent))
        throw new Exception("Failed to receive result from server!");

      if (ReturnEvent.Header == NetworkHeader.HEADER_ERROR)
        throw new Exception("An exception was caused on the server!");
      else if (ReturnEvent.Header != NetworkHeader.HEADER_RETURN)
        throw new Exception("Unexpected error");

      return (T)ReturnEvent.Data;
    }

    public void RemoteCall(string identifier, params object[] param)
    {
      var Event = new NetworkEvents(NetworkHeader.HEADER_CALL, identifier, param);

      if (!ClientSocket.TryBlockingSend(Event))
        throw new Exception("Failed to sent request to server!");

      if (!ClientSocket.TryBlockingReceive(out var ReturnEvent))
        throw new Exception("Failed to receive result from server!");

      if (ReturnEvent.Header == NetworkHeader.HEADER_ERROR)
        throw new Exception("An exception was caused on the server!");
      else if (ReturnEvent.Header != NetworkHeader.HEADER_RETURN)
        throw new Exception("Unexpected error");
    }
  }
}