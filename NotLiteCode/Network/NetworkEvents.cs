using System;
using System.ComponentModel;
using System.Net;

namespace NotLiteCode.Network
{
  public class NetworkEvent
  {
    public readonly NetworkHeader Header;
    public readonly string CallbackGuid;
    public readonly string Tag;
    public readonly object Data;

    public NetworkEvent(NetworkHeader Header, string Tag, object Data)
    {
      this.Header = Header;
      this.CallbackGuid = null;
      this.Tag = Tag;
      this.Data = Data;
    }

    public NetworkEvent(NetworkHeader Header, string CallbackGuid, string Tag, object Data)
    {
      this.Header = Header;
      this.CallbackGuid = CallbackGuid;
      this.Tag = Tag;
      this.Data = Data;
    }

    public static bool TryParse(object[] NetworkMessage, out NetworkEvent Event)
    {
      if (NetworkMessage.Length < 1 || !NetworkMessage[0].TryParseEnum<NetworkHeader>(out var Header))
      {
        Event = default(NetworkEvent);
        return false;
      }
      else
      {
        Event = new NetworkEvent(Header, NetworkMessage[1] as string, NetworkMessage[2] as string, NetworkMessage[3]);
        return true;
      }
    }

    public object[] Package()
    {
      return new object[] { Header, CallbackGuid, Tag, Data };
    }
  }

  public class OnNetworkMessageReceivedEventArgs : AsyncCompletedEventArgs
  {
    public readonly NetworkEvent Message;

    public OnNetworkMessageReceivedEventArgs(NetworkEvent Message) : base(null, false, null)
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