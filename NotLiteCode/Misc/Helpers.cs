using NotLiteCode.Network;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NotLiteCode
{
  public static class Helpers
  {
    public static Task<int> CopyTo(this Stream InputStream, Stream OutputStream)
    {
      byte[] Buffer = new byte[4096];
      int Count;

      while ((Count = InputStream.Read(Buffer, 0, Buffer.Length)) != 0)
        OutputStream.Write(Buffer, 0, Count);

      return Task.FromResult(Count);
    }

    public static Task<T[]> Slice<T>(this T[] source, int start, int length)
    {
      var result = new T[length];
      Array.Copy(source, start, result, 0, length);
      return Task.FromResult(result);
    }

    public static bool TryParseEnum<T>(this object SourceObject, out T EnumValue)
    {
      if (!Enum.IsDefined(typeof(T), SourceObject))
      {
        EnumValue = default(T);
        return false;
      }

      EnumValue = (T)Enum.Parse(typeof(T), SourceObject.ToString());
      return true;
    }

    public class TiedEventWait
    {
      public EventWaitHandle Event = new EventWaitHandle(false, EventResetMode.ManualReset);
      public NetworkEvent Result;
    }
  }
}