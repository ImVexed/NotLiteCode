using System;
using System.Net;

namespace NotLiteCode.Network
{
  public class NetworkEvents
  {
    public readonly NetworkHeader Header;
    public readonly string Tag;
    public readonly object Data;

    public NetworkEvents(NetworkHeader Header, string Tag, object Data)
    {
      this.Header = Header;
      this.Tag = Tag;
      this.Data = Data;
    }

    public static bool TryParse(object[] NetworkMessage, out NetworkEvents Event)
    {
      if (NetworkMessage.Length < 1 || !NetworkMessage[0].TryParseEnum<NetworkHeader>(out var Header))
      {
        Event = default(NetworkEvents);
        return false;
      }
      else
      {
        Event = new NetworkEvents(Header, NetworkMessage[1] as string, NetworkMessage[2]);
        return true;
      }
    }

    public object[] Package()
    {
      return new object[] { Header, Tag, Data };
    }
  }

  public class OnNetworkMessageReceivedEventArgs : EventArgs
  {
    public readonly NetworkEvents Message;

    public OnNetworkMessageReceivedEventArgs(NetworkEvents Message)
    {
      this.Message = Message;
    }
  }

  public class OnNetworkClientConnectedEventArgs : EventArgs
  {
    public readonly NLCSocket Client;

    public OnNetworkClientConnectedEventArgs(NLCSocket Client)
    {
      this.Client = Client;
    }
  }

  public class OnNetworkClientDisconnectedEventArgs : EventArgs
  {
    public readonly EndPoint Client;

    public OnNetworkClientDisconnectedEventArgs(EndPoint Client)
    {
      this.Client = Client;
    }
  }

  public class OnNetworkExceptionOccurredEventArgs : EventArgs
  {
    public readonly Exception Exception;

    public OnNetworkExceptionOccurredEventArgs(Exception Exception)
    {
      this.Exception = Exception;
    }
  }

  public class OnNetworkDataReceivedEventArgs : EventArgs
  {
    public readonly int BytesReceived;

    public OnNetworkDataReceivedEventArgs(int BytesReceived)
    {
      this.BytesReceived = BytesReceived;
    }
  }

  public class OnNetworkDataSentEventArgs : EventArgs
  {
    public readonly int BytesSent;

    public OnNetworkDataSentEventArgs(int BytesSent)
    {
      this.BytesSent = BytesSent;
    }
  }
}