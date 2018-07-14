using NotLiteCode.Encryption;
using NotLiteCode.Network;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace NotLiteCode
{
  public class Server<T> where T : IDisposable, new()
  {
    private Dictionary<string, MethodInfo> RemotingMethods = new Dictionary<string, MethodInfo>();
    public Dictionary<EndPoint, RemoteClient<T>> Clients = new Dictionary<EndPoint, RemoteClient<T>>();
    public NLCSocket ServerSocket;

    public bool EnableDebug { get; set; } = false;

    public Server(NLCSocket ServerSocket)
    {
      this.ServerSocket = ServerSocket;
    }

    public void Start()
    {
      foreach (MethodInfo SharedMethod in typeof(T).GetMethods())
      {
        var SharedMethodAttribute = SharedMethod.GetCustomAttributes(typeof(NLCCall), false);

        if (SharedMethodAttribute.Length > 0)
        {
          var thisAttr = SharedMethodAttribute[0] as NLCCall;

          if (RemotingMethods.ContainsKey(thisAttr.Identifier))
            throw new Exception("There are more than one function inside the SharedClass with the same Identifier!");

          Log($"Identifier {thisAttr.Identifier} MethodInfo link created...", ConsoleColor.Green);
          RemotingMethods.Add(thisAttr.Identifier, SharedMethod);
        }
      }

      ServerSocket.OnClientDisconnected += (x, y) => Log($"Client disconnected!", ConsoleColor.Yellow);
      ServerSocket.OnExceptionOccurred += (x, y) => Log($"Excption! {y.Exception}", ConsoleColor.Red, true);
      ServerSocket.OnClientConnected += OnClientConnected;

      ServerSocket.Listen();
      ServerSocket.BeginAcceptClients();

      Log("Server started!", ConsoleColor.Green);
    }

    public void Stop()
    {
      ServerSocket.Close();
    }

    private void OnClientConnected(object sender, OnClientConnectedEventArgs e)
    {
      if (Encryptor.TrySendHandshake(e.Client, out var ClientEncryptor))
      {
        var Client = new RemoteClient<T>(e.Client, ClientEncryptor);
        e.Client.OnMessageReceived += OnMessageReceived;
        e.Client.OnDataReceived += OnDataReceived;
        e.Client.OnDataSent += OnDataSent;
        e.Client.BeginAcceptMessages(ClientEncryptor);

        Clients.Add(e.Client.BaseSocket.RemoteEndPoint, Client);

        Log($"Client connected from IP: {e.Client.BaseSocket.RemoteEndPoint}", ConsoleColor.Green, true);
      }
    }

    private void OnDataReceived(object sender, OnDataReceivedEventArgs e)
    {
      Log($"Receiving {e.BytesReceived} bytes...", ConsoleColor.Cyan);
    }
    private void OnDataSent(object sender, OnDataSentEventArgs e)
    {
      Log($"Sending {e.BytesSent} bytes...", ConsoleColor.Cyan);
    }

    private void OnMessageReceived(object sender, OnMessageReceivedEventArgs e)
    {
      if (e.Message.Header != NetworkHeader.HEADER_CALL && e.Message.Header != NetworkHeader.HEADER_MOVE)
      {
        Log($"Invalid message type received!", ConsoleColor.Red, true);
        return;
      }

      var RemoteEndPoint = ((NLCSocket)sender).BaseSocket.RemoteEndPoint;

      object Result = null;
      NetworkHeader ResultHeader;

      if (!RemotingMethods.TryGetValue(e.Message.Tag, out var TargetMethod))
      {
        Log("Client attempted to invoke invalid function!", ConsoleColor.Red, true);
        ResultHeader = NetworkHeader.NONE;
      }
      else
      {
        try
        {
          Result = TargetMethod.Invoke(Clients[RemoteEndPoint].SharedClass, (object[])e.Message.Data);
          ResultHeader = NetworkHeader.HEADER_RETURN;
        }
        catch
        {
          Log($"Client IP {RemoteEndPoint} caused an exception invoking {e.Message.Tag}");
          ResultHeader = NetworkHeader.HEADER_ERROR;
        }

        Log($"Client IP: {RemoteEndPoint} called Remote Identifier: {e.Message.Tag}", ConsoleColor.Cyan);
      }

      var Event = new NetworkEvent(ResultHeader, null, Result);

      ((NLCSocket)sender).TryBlockingSend(Event);
    }

    private void Log(string message, ConsoleColor color = ConsoleColor.Gray, bool force = false)
    {
      if (!EnableDebug && !force)
        return;

      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.Write("[{0}] ", DateTime.Now.ToLongTimeString());
      Console.ForegroundColor = color;
      Console.Write("{0}{1}", message, Environment.NewLine);
      Console.ResetColor();
    }
  }

  [AttributeUsage(AttributeTargets.Method)]
  public class NLCCall : Attribute
  {
    public readonly string Identifier;

    public NLCCall(string Identifier)
    {
      this.Identifier = Identifier;
    }
  }

  public class RemoteClient<T> where T : IDisposable, new()
  {
    public NLCSocket Socket;
    public Encryptor Encryptor;
    public T SharedClass;

    public RemoteClient(NLCSocket Socket, Encryptor Encryptor)
    {
      this.Socket = Socket;
      this.Encryptor = Encryptor;
      this.SharedClass = new T();
    }
  }
}