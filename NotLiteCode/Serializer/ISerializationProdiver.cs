using NotLiteCode.Network;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NotLiteCode.Serializer
{
    public interface ISerializationProdiver
    {
        byte[] Serialize<T>(T data);
        T Deserialize<T>(byte[] data);
    }
}
