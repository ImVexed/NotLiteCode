using NotLiteCode.Compression;
using NotLiteCode.Cryptography;
using NotLiteCode.Network;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace NotLiteCode.Server
{
  public class Server<T> where T : IDisposable, new()
  {
    public event EventHandler<OnServerClientDisconnectedEventArgs> OnServerClientDisconnected;

    public event EventHandler<OnServerExceptionOccurredEventArgs> OnServerExceptionOccurred;

    public event EventHandler<OnServerClientConnectedEventArgs> OnServerClientConnected;

    public event EventHandler<OnServerMethodInvokedEventArgs> OnServerMethodInvoked;

    private Dictionary<string, RemotingMethod> RemotingMethods = new Dictionary<string, RemotingMethod>();

    public Dictionary<EndPoint, RemoteClient<T>> Clients = new Dictionary<EndPoint, RemoteClient<T>>();

    public NLCSocket ServerSocket;

    public Server() : this(new NLCSocket(new EncryptorOptions(), new CompressorOptions()))
    { }

    public Server(NLCSocket ServerSocket)
    {
      this.ServerSocket = ServerSocket;
    }

    private void RegisterFunctions()
    {
      foreach (MethodInfo SharedMethod in typeof(T).GetMethods())
      {
        var SharedMethodAttribute = SharedMethod.GetCustomAttributes(typeof(NLCCall), false);

        if (SharedMethodAttribute.Length > 0)
        {
          var thisAttr = SharedMethodAttribute[0] as NLCCall;

          if (RemotingMethods.ContainsKey(thisAttr.Identifier))
          {
            OnServerExceptionOccurred?.Invoke(this, new OnServerExceptionOccurredEventArgs(new Exception($"Method with identifier {thisAttr.Identifier} already exists!")));
            continue;
          }

          RemotingMethods.Add(thisAttr.Identifier, new RemotingMethod() { MethodInfo = SharedMethod, WithContext = thisAttr.WithContext });
        }
      }
    }

    public void Start(int Port = 1337)
    {
      ServerSocket.OnNetworkExceptionOccurred += (x, y) => OnServerExceptionOccurred?.Invoke(this, new OnServerExceptionOccurredEventArgs(y.Exception));
      ServerSocket.OnNetworkClientConnected += OnNetworkClientConnected;

      RegisterFunctions();

      ServerSocket.Listen(Port);
    }

    public void Stop()
    {
      ServerSocket.Close();
    }

    private async void OnNetworkClientConnected(object sender, OnNetworkClientConnectedEventArgs e)
    {
      if (await e.Client.TrySendHandshake())
      {
        var Client = new RemoteClient<T>(e.Client);
        e.Client.OnNetworkClientDisconnected += (x, y) => OnServerClientDisconnected?.Invoke(this, new OnServerClientDisconnectedEventArgs(y.Client));
        e.Client.OnNetworkExceptionOccurred += (x, y) => OnServerExceptionOccurred?.Invoke(this, new OnServerExceptionOccurredEventArgs(y.Exception));
        e.Client.OnNetworkMessageReceived += OnNetworkMessageReceived;
        
        Clients.Add(e.Client.BaseSocket.RemoteEndPoint, Client);

        e.Client.BeginAcceptMessages();

        OnServerClientConnected?.Invoke(this, new OnServerClientConnectedEventArgs(e.Client.BaseSocket.RemoteEndPoint));
      }
    }

    private async void OnNetworkMessageReceived(object sender, OnNetworkMessageReceivedEventArgs e)
    {
      if (e.Message.Header != NetworkHeader.HEADER_CALL && e.Message.Header != NetworkHeader.HEADER_MOVE)
      {
        OnServerExceptionOccurred?.Invoke(this, new OnServerExceptionOccurredEventArgs(new Exception("Invalid message type received!")));
        return;
      }

      var RemoteEndPoint = ((NLCSocket)sender).BaseSocket.RemoteEndPoint;

      object Result = null;
      NetworkHeader ResultHeader;

      if (!RemotingMethods.TryGetValue(e.Message.Tag, out var TargetMethod))
      {
        OnServerExceptionOccurred?.Invoke(this, new OnServerExceptionOccurredEventArgs(new Exception("Client attempted to invoke invalid function!")));
        ResultHeader = NetworkHeader.NONE;
      }
      else
      {
        var Parameters = new List<object>();

        if (TargetMethod.WithContext)
          Parameters.Add(RemoteEndPoint);

        Parameters.AddRange((object[])e.Message.Data);

        try
        {
          Result = TargetMethod.MethodInfo.Invoke(Clients[RemoteEndPoint].SharedClass, Parameters.ToArray());
          ResultHeader = NetworkHeader.HEADER_RETURN;
        }
        catch
        {
          OnServerExceptionOccurred?.Invoke(this, new OnServerExceptionOccurredEventArgs(new Exception($"Client caused an exception invoking {e.Message.Tag}")));
          ResultHeader = NetworkHeader.HEADER_ERROR;
        }

        OnServerMethodInvoked?.Invoke(this, new OnServerMethodInvokedEventArgs(RemoteEndPoint, e.Message.Tag, ResultHeader == NetworkHeader.HEADER_ERROR));
      }

      var Event = new NetworkEvent(ResultHeader, null, Result);

      await ((NLCSocket)sender).BlockingSend(Event);
    }
  }

  [AttributeUsage(AttributeTargets.Method)]
  public class NLCCall : Attribute
  {
    public readonly string Identifier;
    public readonly bool WithContext;

    public NLCCall(string Identifier, bool WithContext = false)
    {
      this.Identifier = Identifier;
      this.WithContext = WithContext;
    }
  }

  public struct RemotingMethod
  {
    public MethodInfo MethodInfo;
    public bool WithContext;
  }
  public class RemoteClient<T> where T : IDisposable, new()
  {
    public NLCSocket Socket;
    public T SharedClass;

    public RemoteClient(NLCSocket Socket)
    {
      this.Socket = Socket;
      this.SharedClass = new T();
    }
  }
}