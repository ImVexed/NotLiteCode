using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

namespace NotLiteCode
{
    internal class Client
                 : Prototypes // Not really necessary, but helps for debugging purposes
    {
        private const string HEADER_CALL = "NLC_CALL";
        private const string HEADER_RETURN = "NLC_RETURN";
        private const string HEADER_HANDSHAKE = "NLC_HANDSHAKE";
        private const string HEADER_MOVE = "NLC_MOVE";

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
        [MethodImpl(MethodImplOptions.NoInlining)] // Without this, Calling StackFrame(1) on release mode will break, however this works fine in Debug, you can alternatively disable optimizations.
        public void Test()
          => RemoteCall(); // RemoteCall< {ReturnType} >( {Any parameters} ) or RemoteCall( {Any parameters} )

        [MethodImpl(MethodImplOptions.NoInlining)]
        public string CombineTwoStringsAndReturn(string s1, string s2)
            => RemoteCall<string>(s1, s2);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SpeedTest()
            => RemoteCall();
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

            if (oRecv[0] as string != HEADER_HANDSHAKE) // Sanity check
                throw new Exception("Unexpected error");

            byte[] sBuf = oRecv[1] as byte[];

            using (var cAlgo = new ECDiffieHellmanCng(cCngKey))
            using (CngKey sPubKey = CngKey.Import(sBuf, CngKeyBlobFormat.EccPublicBlob))
                bKeyTemp = cAlgo.DeriveKeyMaterial(sPubKey);

            send(HEADER_HANDSHAKE, cPublic);
            bKey = bKeyTemp;
        }

        public T RemoteCall<T>(params object[] param)
        {
            object[] payload = new object[param.Length + 2]; // +2 for header & method name
            payload[0] = HEADER_MOVE;
            payload[1] = new StackFrame(1).GetMethod().Name;
            Array.Copy(param, 0, payload, 2, param.Length);
            send(payload);
            object[] oRecv = receive();

            if (oRecv[0] as string != HEADER_RETURN)
                throw new Exception("Unexpected error");

            return (T)oRecv[1];
        }

        public void RemoteCall(params object[] param)
        {
            object[] payload = new object[param.Length + 2]; // +2 for header & method name
            payload[0] = HEADER_CALL;
            payload[1] = new StackFrame(1).GetMethod().Name;
            Array.Copy(param, 0, payload, 2, param.Length);
            send(payload);
            object[] oRecv = receive();

            if (oRecv[0] as string != HEADER_RETURN)
                throw new Exception("Unexpected error");
        }

        private object[] receive()
        {
            byte[] bSize = new byte[4];
            cS.Receive(bSize);

            byte[] sBuf = new byte[BitConverter.ToInt32(bSize, 0)];
            cS.Receive(sBuf);

            if (bKey != null)
                sBuf = AES_Decrypt(sBuf, bKey);

            return Formatter.Deserialize<object[]>(sBuf);
        }

        private void send(params object[] param)
        {
            byte[] bSend = Formatter.Serialize(param);
            if (bKey != null)
                bSend = AES_Encrypt(bSend, bKey);

            cS.Send(BitConverter.GetBytes(bSend.Length)); // Send expected payload length, gets bytes of int representing size, will always be 4 bytes for Int32
            cS.Send(bSend);
        }

        public byte[] AES_Encrypt(byte[] bytesToBeEncrypted, byte[] passwordBytes)
        // Generic AES 256 class from StackOverFlow, modified to have random IV/Salt
        {
            byte[] encryptedBytes = null;

            byte[] saltBytes = new byte[16];
            cRandom.GetBytes(saltBytes);

            using (MemoryStream ms = new MemoryStream())
            {
                using (RijndaelManaged AES = new RijndaelManaged())
                {
                    AES.KeySize = 256;
                    AES.BlockSize = 128;

                    var key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, 1000);
                    AES.Key = key.GetBytes(AES.KeySize / 8);
                    AES.IV = key.GetBytes(AES.BlockSize / 8);

                    AES.Mode = CipherMode.CBC;

                    using (var cs = new CryptoStream(ms, AES.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);
                        cs.Close();
                    }
                    encryptedBytes = ms.ToArray();
                }
            }

            return encryptedBytes.Concat(saltBytes).ToArray();
        }

        public byte[] AES_Decrypt(byte[] bytes, byte[] passwordBytes)
        {
            byte[] decryptedBytes = null;
            byte[] bytesToBeDecrypted = new byte[bytes.Length - 16];

            byte[] saltBytes = new byte[16];

            Array.Copy(bytes, bytes.Length - 16, saltBytes, 0, 16);
            Array.Copy(bytes, bytesToBeDecrypted, bytes.Length - 16);

            using (MemoryStream ms = new MemoryStream())
            {
                using (RijndaelManaged AES = new RijndaelManaged())
                {
                    AES.KeySize = 256;
                    AES.BlockSize = 128;

                    var key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, 1000);
                    AES.Key = key.GetBytes(AES.KeySize / 8);
                    AES.IV = key.GetBytes(AES.BlockSize / 8);

                    AES.Mode = CipherMode.CBC;

                    using (var cs = new CryptoStream(ms, AES.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytesToBeDecrypted, 0, bytesToBeDecrypted.Length);
                        cs.Close();
                    }
                    decryptedBytes = ms.ToArray();
                }
            }

            return decryptedBytes;
        }
    }

    public static class Formatter // Shamelessly stolen from BahNahNah on HF & wherever he stole it from
    {
        public static byte[] Serialize(object input)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, input);
                return Compress(ms.ToArray());
            }
        }

        public static t Deserialize<t>(byte[] input)
        {
            try
            {
                BinaryFormatter bf = new BinaryFormatter();
                using (MemoryStream ms = new MemoryStream(Decompress(input)))
                {
                    return (t)bf.Deserialize(ms);
                }
            }
            catch
            {
                return default(t);
            }
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
}