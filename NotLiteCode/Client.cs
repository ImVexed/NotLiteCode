using NotLiteCode.Encryption;
using NotLiteCode.Network;
using System;

namespace NotLiteCode
{
  public class Client
  {
    private NLCSocket ClientSocket;

    public bool EnableDebug { get; set; } = false;

    public Client(NLCSocket ClientSocket)
    {
      this.ClientSocket = ClientSocket;
    }

    public bool Connect(string ServerAddress, int ServerPort)
    {
      ClientSocket.Connect(ServerAddress, ServerPort);

      if (Encryptor.TryReceiveHandshake(ClientSocket, out ClientSocket.Encryptor))
      {
        Console.WriteLine("Handshake Complete!");
        return true;
      }
      else
        return false;
    }

    public void Stop()
    {
      ClientSocket.Close();
    }

    public T RemoteCall<T>(string identifier, params object[] param)
    {
      var Event = new NetworkEvent(NetworkHeader.HEADER_MOVE, identifier, param);

      Log(String.Format("Calling remote method: {0}", identifier), ConsoleColor.Cyan);

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
      var Event = new NetworkEvent(NetworkHeader.HEADER_CALL, identifier, param);

      Log(String.Format("Calling remote method: {0}", identifier), ConsoleColor.Cyan);

      if (!ClientSocket.TryBlockingSend(Event))
        throw new Exception("Failed to sent request to server!");

      if (!ClientSocket.TryBlockingReceive(out var ReturnEvent))
        throw new Exception("Failed to receive result from server!");

      if (ReturnEvent.Header == NetworkHeader.HEADER_ERROR)
        throw new Exception("An exception was caused on the server!");
      else if (ReturnEvent.Header != NetworkHeader.HEADER_RETURN)
        throw new Exception("Unexpected error");
    }

    private void Log(string message, ConsoleColor color = ConsoleColor.Gray)
    {
      if (!EnableDebug)
        return;

      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.Write("[{0}] ", DateTime.Now.ToLongTimeString());
      Console.ForegroundColor = color;
      Console.Write("{0}{1}", message, Environment.NewLine);
      Console.ResetColor();
    }
  }
}