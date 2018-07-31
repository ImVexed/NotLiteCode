using NotLiteCode.Compression;
using NotLiteCode.Cryptography;
using NotLiteCode.Network;
using System;
using System.Threading.Tasks;

namespace NotLiteCode
{
  public class Client
  {
    private NLCSocket ClientSocket;

    public Client() : this(new NLCSocket(new EncryptorOptions(), new CompressorOptions()))
    { }

    public Client(NLCSocket ClientSocket)
    {
      this.ClientSocket = ClientSocket;
    }

    public async Task<bool> Connect(string ServerAddress, int ServerPort)
    {
      await ClientSocket.Connect(ServerAddress, ServerPort);

      return await ClientSocket.TryReceiveHandshake();
    }

    public void Stop()
    {
      ClientSocket.Close();
    }

    public async Task<T> RemoteCall<T>(string identifier, params object[] param)
    {
      var Event = new NetworkEvent(NetworkHeader.HEADER_MOVE, identifier, param);

      if (!await ClientSocket.BlockingSend(Event))
        throw new Exception("Failed to sent request to server!");

      NetworkEvent ReturnEvent;

      if ((ReturnEvent = await ClientSocket.BlockingReceive()) == default(NetworkEvent))
        throw new Exception("Failed to receive result from server!");

      if (ReturnEvent.Header == NetworkHeader.HEADER_ERROR)
        throw new Exception("An exception was caused on the server!");
      else if (ReturnEvent.Header != NetworkHeader.HEADER_RETURN)
        throw new Exception("Unexpected error");

      return (T)ReturnEvent.Data;
    }

    public async Task RemoteCall(string identifier, params object[] param)
    {
      var Event = new NetworkEvent(NetworkHeader.HEADER_CALL, identifier, param);

      if (!await ClientSocket.BlockingSend(Event))
        throw new Exception("Failed to sent request to server!");

      NetworkEvent ReturnEvent;

      if ((ReturnEvent = await ClientSocket.BlockingReceive()) == default(NetworkEvent))
        throw new Exception("Failed to receive result from server!");

      if (ReturnEvent.Header == NetworkHeader.HEADER_ERROR)
        throw new Exception("An exception was caused on the server!");
      else if (ReturnEvent.Header != NetworkHeader.HEADER_RETURN)
        throw new Exception("Unexpected error");
    }
  }
}