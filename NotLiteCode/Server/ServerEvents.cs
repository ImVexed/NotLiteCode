using System;
using System.Net;

namespace NotLiteCode.Server
{
  public class OnServerClientConnectedEventArgs : EventArgs
  {
    public readonly EndPoint Client;

    public OnServerClientConnectedEventArgs(EndPoint Client)
    {
      this.Client = Client;
    }
  }

  public class OnServerClientDisconnectedEventArgs : EventArgs
  {
    public readonly EndPoint Client;

    public OnServerClientDisconnectedEventArgs(EndPoint Client)
    {
      this.Client = Client;
    }
  }

  public class OnServerMethodInvokedEventArgs : EventArgs
  {
    public readonly EndPoint Client;
    public readonly string Identifier;
    public readonly bool WasErroneous;

    public OnServerMethodInvokedEventArgs(EndPoint Client, string Identifier, bool WasErroneous)
    {
      this.Client = Client;
      this.Identifier = Identifier;
      this.WasErroneous = WasErroneous;
    }
  }

  public class OnServerExceptionOccurredEventArgs : EventArgs
  {
    public readonly Exception Exception;

    public OnServerExceptionOccurredEventArgs(Exception Exception)
    {
      this.Exception = Exception;
    }
  }
}