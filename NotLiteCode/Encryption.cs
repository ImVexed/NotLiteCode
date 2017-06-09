using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

namespace NotLiteCode
{
    public enum HASH_STRENGTH
    {
        LOW = 1000,
        MEDIUM = 10000,
        HIGH = 20000
    }

    public class Encryption
    {
        public static int iCompressionLevel = 1;

        private RNGCryptoServiceProvider cRandom = new RNGCryptoServiceProvider();

        private AesCryptoServiceProvider AES = new AesCryptoServiceProvider();

        private byte[] bKey;

        private HASH_STRENGTH strength;

        public Encryption(byte[] key, HASH_STRENGTH strength)
        {
            bKey = key;
            AES.KeySize = 256;
            AES.BlockSize = 128;
            AES.Mode = CipherMode.CBC;
            AES.Padding = PaddingMode.PKCS7;
            this.strength = strength;
        }

        public byte[] AES_Encrypt(byte[] bytesToBeEncrypted)
        {
            byte[] bOut;
            byte[] bIV = new byte[16];

            cRandom.GetBytes(bIV);

            var key = new Rfc2898DeriveBytes(bKey, bIV, (int)strength);
            byte[] bHKey = key.GetBytes(AES.KeySize / 8);
            byte[] bHIV = key.Salt;

            AES.Key = bHKey;
            AES.IV = bHIV;

            using (var iCT = AES.CreateEncryptor())
                bOut = iCT.TransformFinalBlock(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);

            byte[] bFinal = new byte[bOut.Length + 32 + 16];

            Array.Copy(bHIV, 0, bFinal, 0, 16);

            using (var hmac = new HMACSHA256(bHKey))
                Array.Copy(hmac.ComputeHash(bOut), 0, bFinal, 16, 32);

            Array.Copy(bOut, 0, bFinal, 16 + 32, bOut.Length);
            return Compress(bFinal);
        }

        public byte[] AES_Decrypt(byte[] bytes)
        {
            byte[] decryptedBytes;
            byte[] bIn = Decompress(bytes);
            byte[] bIV = new byte[16];
            byte[] bHash = new byte[32];
            byte[] bPayload = new byte[bIn.Length - 16 - 32];

            Array.Copy(bIn, bIV, 16);
            Array.Copy(bIn, 16, bHash, 0, 32);
            Array.Copy(bIn, 16 + 32, bPayload, 0, bIn.Length - 16 - 32);

            var key = new Rfc2898DeriveBytes(bKey, bIV, (int)strength);
            byte[] bHKey = key.GetBytes(AES.KeySize / 8);
            byte[] bHIV = key.Salt;

            AES.Key = bHKey;
            AES.IV = bHIV;

            using (var hmac = new HMACSHA256(bHKey))
                if (!hmac.ComputeHash(bPayload).SequenceEqual(bHash))
                    throw new Exception("Data has been modified! Oracle padding attack? Who cares! Run!");

            using (var iCT = AES.CreateDecryptor())
                decryptedBytes = iCT.TransformFinalBlock(bPayload, 0, bPayload.Length);

            return decryptedBytes;
        }

        public static byte[] Compress(byte[] input)
        {
            using (var ps = new MemoryStream(input))
            using (var ms = new MemoryStream())
            {
                using (var df = new DeflateStream(ms, CompressionMode.Compress))
                {
                    ps.WriteTo(df);
                }
                return ms.ToArray();
            }
        }

        public static byte[] Decompress(byte[] input)
        {
            using (var ps = new MemoryStream(input))
            using (var ms = new MemoryStream())
            {
                using (var df = new DeflateStream(ps, CompressionMode.Decompress))
                {
                    df.CopyTo(ms);
                }
                return ms.ToArray();
            }
        }

        public class BinaryFormatterSerializer
        {
            public static byte[] Serialize(object Message)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(stream, Message);
                    return stream.ToArray();
                }
            }

            public static object[] Deserialize(byte[] MessageData)
            {
                using (MemoryStream stream = new MemoryStream(MessageData))
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    return bf.Deserialize(stream) as object[];
                }
            }
        }
    }
}