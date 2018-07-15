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

    private Dictionary<string, MethodInfo> RemotingMethods = new Dictionary<string, MethodInfo>();

    public Dictionary<EndPoint, RemoteClient<T>> Clients = new Dictionary<EndPoint, RemoteClient<T>>();

    public NLCSocket ServerSocket;
    

    public Server() : this(new NLCSocket(new EncryptionOptions(), new CompressorOptions()))
    {}

    public Server(NLCSocket ServerSocket)
    {    
      this.ServerSocket = ServerSocket;
      
      RegisterFunctions();
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
            OnServerExceptionOccurred?.BeginInvoke(this, new OnServerExceptionOccurredEventArgs(new Exception($"Method with identifier {thisAttr.Identifier} already exists!")), null, null);
            continue;
          }

          RemotingMethods.Add(thisAttr.Identifier, SharedMethod);
        }
      }
    }

    public void Start()
    {
      ServerSocket.OnNetworkClientDisconnected += (x, y) => OnServerClientDisconnected?.BeginInvoke(this, new OnServerClientDisconnectedEventArgs(y.Client), null, null);
      ServerSocket.OnNetworkExceptionOccurred += (x, y) => OnServerExceptionOccurred?.BeginInvoke(this, new OnServerExceptionOccurredEventArgs(y.Exception), null, null);
      ServerSocket.OnNetworkClientConnected += OnNetworkClientConnected;

      ServerSocket.Listen();
      ServerSocket.BeginAcceptClients();
    }

    public void Stop()
    {
      ServerSocket.Close();
    }

    private void OnNetworkClientConnected(object sender, OnNetworkClientConnectedEventArgs e)
    {
      if (e.Client.TrySendHandshake())
      {
        var Client = new RemoteClient<T>(e.Client);
        e.Client.OnNetworkMessageReceived += OnNetworkMessageReceived;
        e.Client.BeginAcceptMessages();

        Clients.Add(e.Client.BaseSocket.RemoteEndPoint, Client);

        OnServerClientConnected?.BeginInvoke(this, new OnServerClientConnectedEventArgs(e.Client.BaseSocket.RemoteEndPoint), null, null);
      }
    }

    private void OnNetworkMessageReceived(object sender, OnNetworkMessageReceivedEventArgs e)
    {
      if (e.Message.Header != NetworkHeader.HEADER_CALL && e.Message.Header != NetworkHeader.HEADER_MOVE)
      {
        OnServerExceptionOccurred?.BeginInvoke(this, new OnServerExceptionOccurredEventArgs(new Exception("Invalid message type received!")), null, null);
        return;
      }

      var RemoteEndPoint = ((NLCSocket)sender).BaseSocket.RemoteEndPoint;

      object Result = null;
      NetworkHeader ResultHeader;

      if (!RemotingMethods.TryGetValue(e.Message.Tag, out var TargetMethod))
      {
        OnServerExceptionOccurred?.BeginInvoke(this, new OnServerExceptionOccurredEventArgs(new Exception("Client attempted to invoke invalid function!")), null, null);
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
          OnServerExceptionOccurred?.BeginInvoke(this, new OnServerExceptionOccurredEventArgs(new Exception($"Client caused an exception invoking {e.Message.Tag}")), null, null);
          ResultHeader = NetworkHeader.HEADER_ERROR;
        }

        OnServerMethodInvoked?.BeginInvoke(this, new OnServerMethodInvokedEventArgs(RemoteEndPoint, e.Message.Tag, ResultHeader == NetworkHeader.HEADER_ERROR), null, null);
      }

      var Event = new NetworkEvents(ResultHeader, null, Result);

      ((NLCSocket)sender).TryBlockingSend(Event);
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
    public T SharedClass;

    public RemoteClient(NLCSocket Socket)
    {
      this.Socket = Socket;
      this.SharedClass = new T();
    }
  }
}