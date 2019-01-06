using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace NotLiteCode.Misc
{
    public class Serializer
    {
        public static byte[] Serialize(params object[] Message)
        {
            using (MemoryStream OutputStream = new MemoryStream())
            {
                BinaryFormatter Formatter = new BinaryFormatter();
                Formatter.Serialize(OutputStream, Message);
                return OutputStream.ToArray();
            }
        }

        public static object[] Deserialize(byte[] MessageData)
        {
            using (MemoryStream OutputStream = new MemoryStream(MessageData))
            {
                BinaryFormatter Formatter = new BinaryFormatter();
                return Formatter.Deserialize(OutputStream) as object[];
            }
        }
    }
}