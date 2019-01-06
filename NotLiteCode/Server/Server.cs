using NotLiteCode.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace NotLiteCode.Server
{
    public class Server<T> where T : IDisposable, new()
    {
        private void NetworkClientDisconnected(object sender, OnNetworkClientDisconnectedEventArgs e) =>
          OnServerClientDisconnected?.Invoke(this, new OnServerClientDisconnectedEventArgs(e.Client));

        private void NetworkExceptionOccurred(object sender, OnNetworkExceptionOccurredEventArgs e) =>
          OnServerExceptionOccurred?.Invoke(this, new OnServerExceptionOccurredEventArgs(e.Exception));

        public event EventHandler<OnServerClientDisconnectedEventArgs> OnServerClientDisconnected;

        public event EventHandler<OnServerExceptionOccurredEventArgs> OnServerExceptionOccurred;

        public event EventHandler<OnServerClientConnectedEventArgs> OnServerClientConnected;

        public event EventHandler<OnServerMethodInvokedEventArgs> OnServerMethodInvoked;

        private Dictionary<string, RemotingMethod> RemotingMethods = new Dictionary<string, RemotingMethod>();

        public Dictionary<EndPoint, RemoteClient<T>> Clients = new Dictionary<EndPoint, RemoteClient<T>>();

        public NLCSocket ServerSocket;

        public Server() : this(new NLCSocket())
        { }

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

            ServerSocket.Listen(Port);
        }

        public void Stop()
        {
            ServerSocket.Close();
        }

        public void ManuallyConnectSocket(NLCSocket Socket)
        {
            OnNetworkClientConnected(null, new OnNetworkClientConnectedEventArgs(Socket));
        }

        private void OnNetworkClientConnected(object sender, OnNetworkClientConnectedEventArgs e)
        {
            var Client = new RemoteClient<T>(e.Client);
            e.Client.OnNetworkClientDisconnected += NetworkClientDisconnected;
            e.Client.OnNetworkExceptionOccurred += NetworkExceptionOccurred;
            e.Client.OnNetworkMessageReceived += OnNetworkMessageReceived;

            Clients.Add(e.Client.BaseSocket.RemoteEndPoint, Client);

            e.Client.BeginAcceptMessages();

            OnServerClientConnected?.Invoke(this, new OnServerClientConnectedEventArgs(e.Client.BaseSocket.RemoteEndPoint));
        }

        public void DetatchFromSocket(NLCSocket Socket)
        {
            Socket.OnNetworkClientDisconnected -= NetworkClientDisconnected;
            Socket.OnNetworkExceptionOccurred -= NetworkExceptionOccurred;
            Socket.OnNetworkMessageReceived -= OnNetworkMessageReceived;
        }

        private void OnNetworkMessageReceived(object sender, OnNetworkMessageReceivedEventArgs e)
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

                TimeSpan Duration = default(TimeSpan);

                try
                {
                    var sw = Stopwatch.StartNew();

                    Result = TargetMethod.MethodInfo.Invoke(Clients[RemoteEndPoint].SharedClass, Parameters.ToArray());

                    sw.Stop();

                    Duration = sw.Elapsed;

                    ResultHeader = NetworkHeader.HEADER_RETURN;
                }
                catch (Exception ex)
                {
                    OnServerExceptionOccurred?.Invoke(this, new OnServerExceptionOccurredEventArgs(ex));
                    ResultHeader = NetworkHeader.HEADER_ERROR;
                }

                OnServerMethodInvoked?.Invoke(this, new OnServerMethodInvokedEventArgs(RemoteEndPoint, e.Message.Tag, Duration, ResultHeader == NetworkHeader.HEADER_ERROR));
            }

            var Event = new NetworkEvent(ResultHeader, e.Message.CallbackGuid, null, Result);

            ((NLCSocket)sender).BlockingSend(Event);
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