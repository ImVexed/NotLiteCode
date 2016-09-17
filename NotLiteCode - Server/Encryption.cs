using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

namespace NotLiteCode___Server
{
    public class Encryption
    {
        private RNGCryptoServiceProvider cRandom = new RNGCryptoServiceProvider(DateTime.Now.ToString());

        private AesCryptoServiceProvider AES = new AesCryptoServiceProvider();

        private byte[] bKey;

        public Encryption(byte[] key)
        {
            bKey = key;
            AES.KeySize = 256;
            AES.BlockSize = 128;
            AES.Mode = CipherMode.CBC;
        }

        public byte[] AES_Encrypt(byte[] bytesToBeEncrypted)
        {
            object[] oOut = new object[3];
            byte[] bIV = new byte[16];
            cRandom.GetBytes(bIV);
            oOut[0] = bIV;

            var key = new PasswordDeriveBytes(bKey, oOut[0] as byte[]);

            byte[] bHKey = key.GetBytes(AES.KeySize / 8);
            byte[] bHIV = key.GetBytes(AES.BlockSize / 8);

            AES.Key = bHKey;
            AES.IV = bHIV;

            using (var iCT = AES.CreateEncryptor())
            {
                oOut[2] = iCT.TransformFinalBlock(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);
            }

            oOut[1] = new HMACSHA256(bHKey).ComputeHash(oOut[2] as byte[]);

            return Compress(BinaryFormatterSerializer.Serialize(oOut));
        }

        public byte[] AES_Decrypt(byte[] bytes)
        {
            byte[] decryptedBytes = null;
            object[] oIn = BinaryFormatterSerializer.Deserialize(Decompress(bytes));

            var key = new PasswordDeriveBytes(bKey, oIn[0] as byte[]);

            byte[] bHKey = key.GetBytes(AES.KeySize / 8);
            byte[] bHIV = key.GetBytes(AES.BlockSize / 8);

            AES.Key = bHKey;
            AES.IV = bHIV;

            if (!new HMACSHA256(bHKey).ComputeHash(oIn[2] as byte[]).SequenceEqual(oIn[1] as byte[]))
                throw new Exception("Data has been modified! Oracle padding attack? Who cares! Run!");

            byte[] bytesToBeDecrypted = oIn[2] as byte[];

            using (var iCT = AES.CreateDecryptor())
            {
                decryptedBytes = iCT.TransformFinalBlock(bytesToBeDecrypted, 0, bytesToBeDecrypted.Length);
            }

            return decryptedBytes;
        }

        public static byte[] Compress(byte[] input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream _gz = new GZipStream(ms, CompressionMode.Compress))
                {
                    _gz.Write(input, 0, input.Length);
                }
                return ms.ToArray();
            }
        }

        public static byte[] Decompress(byte[] input)
        {
            using (MemoryStream decompressed = new MemoryStream())
            {
                using (MemoryStream ms = new MemoryStream(input))
                {
                    using (GZipStream _gz = new GZipStream(ms, CompressionMode.Decompress))
                    {
                        byte[] Bytebuffer = new byte[1024];
                        int bytesRead = 0;
                        while ((bytesRead = _gz.Read(Bytebuffer, 0, Bytebuffer.Length)) > 0)
                        {
                            decompressed.Write(Bytebuffer, 0, bytesRead);
                        }
                    }
                    return decompressed.ToArray();
                }
            }
        }
    }

    public class BinaryFormatterSerializer
    {
        public static byte[] Serialize(object Message)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Binder = new DeserializationBinder();
                bf.Serialize(stream, Message);
                return stream.ToArray();
            }
        }

        public static object[] Deserialize(byte[] MessageData)
        {
            using (MemoryStream stream = new MemoryStream(MessageData))
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Binder = new DeserializationBinder();
                return bf.Deserialize(stream) as object[];
            }
        }

        private sealed class DeserializationBinder : SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                Type typeToDeserialize = null;

                String exeAssembly = Assembly.GetExecutingAssembly().FullName;

                typeToDeserialize = Type.GetType(String.Format("{0}, {1}", typeName, exeAssembly));

                return typeToDeserialize;
            }
        }
    }
}