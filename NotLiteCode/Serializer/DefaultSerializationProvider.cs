using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace NotLiteCode.Serializer
{
    internal class DefaultSerializationProvider : ISerializationProdiver
    {
        private BinaryFormatter Serializer = new BinaryFormatter();

        public byte[] Serialize<T>(T data)
        {
            using (MemoryStream OutputStream = new MemoryStream())
            {
                Serializer.Serialize(OutputStream, data);
                return OutputStream.ToArray();
            }
        }

        public T Deserialize<T>(byte[] MessageData)
        {
            using (MemoryStream OutputStream = new MemoryStream(MessageData))
                return (T)Serializer.Deserialize(OutputStream);
        }
    }
}