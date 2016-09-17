using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

namespace NotLiteCode
{
    public static class Headers
    {
        public const byte HEADER_CALL = 0x01;
        public const byte HEADER_RETURN = 0x02;
        public const byte HEADER_HANDSHAKE = 0x03;
        public const byte HEADER_MOVE = 0x04;
    }

    internal class Client
                 : Prototypes // Not really necessary, but helps for debugging purposes
    {
        private Socket cS = null;
        private RNGCryptoServiceProvider cRandom = new RNGCryptoServiceProvider(DateTime.Now.ToString());

        #region Variables

        /// <summary>
        /// Port for the client to connect, changing this variable while the client is connected will have no effect.
        /// </summary>
        public int iPort { get; set; }

        /// <summary>
        /// IP for the client to connect, changing this variable while the client is connected will have no effect.
        /// </summary>
        public string sIP { get; set; }

        /// <summary>
        /// Private key for encryption established in the initial handshake, changing this by force will break communications with server.
        /// </summary>
        public byte[] bKey { get; private set; }

        #endregion Variables

        #region Prototypes

        // Override of Prototypes.Test
        public void Test()//                                         Identifier is defined in the server's Shared Class for that method
          => RemoteCall("JustATest"); // RemoteCall< {ReturnType} >( Identifier, {Any parameters} ) or RemoteCall( {Any parameters} )

        public string CombineTwoStringsAndReturn(string s1, string s2)
            => RemoteCall<string>("Pinocchio", s1, s2);

        public void SpeedTest()
            => RemoteCall("Sanic");

        #endregion Prototypes

        /// <summary>
        /// Initializes the NLC Client. Credits to Killpot :^)
        /// </summary>
        /// <param name="sIP">IP of server to connect to.</param>
        /// <param name="iPort">Port of server to connect to.</param>
        public Client(string sIP = "localhost", int iPort = 1337)
        {
            this.sIP = sIP;
            this.iPort = iPort;
            cS = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        /// Connect to the Server and commence the OnConnect routine.
        /// </summary>
        public void Start()
        {
            cS.Connect(sIP, iPort);
            OnConnect();
        }

        /// <summary>
        /// Close connection to the Server.
        /// </summary>
        public void Stop()
        {
            cS.Close();
        }

        private void OnConnect()
        {
            BeginEncrypting(); // Starts the encryption process, you can also add whatever else you want to happen on connect here (Ex. Send HWID or Key)
        }

        private void BeginEncrypting()
        //Our handshake routine
        {
            byte[] bKeyTemp;

            CngKey cCngKey = CngKey.Create(CngAlgorithm.ECDiffieHellmanP521);
            byte[] cPublic = cCngKey.Export(CngKeyBlobFormat.EccPublicBlob);

            object[] oRecv = receive();
            if (!oRecv[0].Equals(Headers.HEADER_HANDSHAKE)) // Sanity check
                throw new Exception("Unexpected error");

            byte[] sBuf = oRecv[1] as byte[];

            using (var cAlgo = new ECDiffieHellmanCng(cCngKey))
            using (CngKey sPubKey = CngKey.Import(sBuf, CngKeyBlobFormat.EccPublicBlob))
                bKeyTemp = cAlgo.DeriveKeyMaterial(sPubKey);

            send(Headers.HEADER_HANDSHAKE, cPublic);
            bKey = bKeyTemp;
        }

        public T RemoteCall<T>(string identifier, params object[] param)
        {
            object[] payload = new object[param.Length + 2]; // +2 for header & method name
            payload[0] = Headers.HEADER_MOVE;
            payload[1] = identifier;
            Array.Copy(param, 0, payload, 2, param.Length);
            send(payload);
            object[] oRecv = receive();

            if (!oRecv[0].Equals(Headers.HEADER_RETURN))
                throw new Exception("Unexpected error");

            return (T)oRecv[1];
        }

        public void RemoteCall(string identifier, params object[] param)
        {
            object[] payload = new object[param.Length + 2]; // +2 for header & method name
            payload[0] = Headers.HEADER_CALL;
            payload[1] = identifier;
            Array.Copy(param, 0, payload, 2, param.Length);
            send(payload);
            object[] oRecv = receive();

            if (!oRecv[0].Equals(Headers.HEADER_RETURN))
                throw new Exception("Unexpected error");
        }

        private object[] receive()
        {
            byte[] bSize = new byte[4];
            cS.Receive(bSize);

            byte[] sBuf = new byte[BitConverter.ToInt32(bSize, 0)];
            cS.Receive(sBuf);

            if (sBuf.Length <= 0)
                throw new Exception("Invalid data length, did the server force disconnect you?");

            if (bKey != null)
                sBuf = AES_Decrypt(sBuf, bKey);
            else
                sBuf = Decompress(sBuf);

            return BinaryFormatterSerializer.Deserialize(sBuf);
        }

        private void send(params object[] param)
        {
            byte[] bSend = BinaryFormatterSerializer.Serialize(param);
            if (bKey != null)
                bSend = AES_Encrypt(bSend, bKey);
            else
                bSend = Compress(bSend);

            cS.Send(BitConverter.GetBytes(bSend.Length)); // Send expected payload length, gets bytes of int representing size, will always be 4 bytes for Int32
            cS.Send(bSend);
        }

        public byte[] AES_Encrypt(byte[] bytesToBeEncrypted, byte[] passwordBytes)
        {
            object[] oOut = new object[3];
            byte[] bIV = new byte[16];
            cRandom.GetBytes(bIV);

            oOut[0] = bIV;

            using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider())
            {
                AES.KeySize = 256;
                AES.BlockSize = 128;

                var key = new Rfc2898DeriveBytes(passwordBytes, oOut[0] as byte[], 1000);

                AES.Mode = CipherMode.CBC;

                byte[] bHKey = key.GetBytes(AES.KeySize / 8);
                byte[] bHIV = key.GetBytes(AES.BlockSize / 8);

                AES.Key = bHKey;
                AES.IV = bHIV;

                using (var iCT = AES.CreateEncryptor())
                {
                    oOut[2] = iCT.TransformFinalBlock(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);
                }

                oOut[1] = new HMACSHA256(bHKey).ComputeHash(oOut[2] as byte[]);
            }

            return Compress(BinaryFormatterSerializer.Serialize(oOut));
        }

        public byte[] AES_Decrypt(byte[] bytes, byte[] passwordBytes)
        {
            byte[] decryptedBytes = null;
            object[] oIn = BinaryFormatterSerializer.Deserialize(Decompress(bytes));

            using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider())
            {
                AES.KeySize = 256;
                AES.BlockSize = 128;

                var key = new Rfc2898DeriveBytes(passwordBytes, oIn[0] as byte[], 1000);

                AES.Mode = CipherMode.CBC;

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
            }

            return decryptedBytes;
        }

        private byte[] Compress(byte[] input)
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

        private byte[] Decompress(byte[] input)
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

    public static class BinaryFormatterSerializer
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

                // For each assemblyName/typeName that you want to deserialize to
                // a different type, set typeToDeserialize to the desired type.
                String exeAssembly = Assembly.GetExecutingAssembly().FullName;

                // The following line of code returns the type.
                typeToDeserialize = Type.GetType(String.Format("{0}, {1}", typeName, exeAssembly));

                return typeToDeserialize;
            }
        }
    }
}