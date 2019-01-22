using NotLiteCode.Serializer;
using GB = GroBuf;
using GroBuf.DataMembersExtracters;
using GroBuf;

namespace NotLiteCode.Serialization
{
    public class GroBufSerializationProvider : ISerializationProdiver
    {
        private GB.Serializer serializer = new GB.Serializer(new PropertiesExtractor(), options : GroBufOptions.WriteEmptyObjects);

        public T Deserialize<T>(byte[] data)
        {
            return serializer.Deserialize<T>(data);
        }

        public byte[] Serialize<T>(T data)
        {
            return serializer.Serialize(data);
        }
    }
}
