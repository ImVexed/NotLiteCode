using System;

namespace NotLiteCode.Network
{
  public class NetworkEvent
  {
    public readonly NetworkHeader Header;
    public readonly string Tag;
    public readonly object Data;

    public NetworkEvent(NetworkHeader Header, string Tag, object Data)
    {
      this.Header = Header;
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
        Event = new NetworkEvent(Header, NetworkMessage[1] as string, NetworkMessage[2]);
        return true;
      }
    }

    public object[] Package()
    {
      return new object[] { Header, Tag, Data };
    }
  }

  public class OnMessageReceivedEventArgs : EventArgs
  {
    public readonly NetworkEvent Message;

    public OnMessageReceivedEventArgs(NetworkEvent Message)
    {
      this.Message = Message;
    }
  }

  public class OnClientConnectedEventArgs : EventArgs
  {
    public readonly NLCSocket Client;

    public OnClientConnectedEventArgs(NLCSocket Client)
    {
      this.Client = Client;
    }
  }

  public class OnExceptionOccurredEventArgs : EventArgs
  {
    public readonly Exception Exception;

    public OnExceptionOccurredEventArgs(Exception Exception)
    {
      this.Exception = Exception;
    }
  }

  public class OnDataReceivedEventArgs : EventArgs
  {
    public readonly int BytesReceived;

    public OnDataReceivedEventArgs(int BytesReceived)
    {
      this.BytesReceived = BytesReceived;
    }
  }

  public class OnDataSentEventArgs : EventArgs
  {
    public readonly int BytesSent;

    public OnDataSentEventArgs(int BytesSent)
    {
      this.BytesSent = BytesSent;
    }
  }
}