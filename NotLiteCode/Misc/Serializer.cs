using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace NotLiteCode.Misc
{
  public class Serializer
  {
    public static Task<byte[]> Serialize(params object[] Message)
    {
      using (MemoryStream OutputStream = new MemoryStream())
      {
        BinaryFormatter Formatter = new BinaryFormatter();
        Formatter.Serialize(OutputStream, Message);
        return Task.FromResult(OutputStream.ToArray());
      }
    }

    public static Task<object[]> Deserialize(byte[] MessageData)
    {
      using (MemoryStream OutputStream = new MemoryStream(MessageData))
      {
        BinaryFormatter Formatter = new BinaryFormatter();
        return Task.FromResult(Formatter.Deserialize(OutputStream) as object[]);
      }
    }
  }
}