using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

namespace NotLiteCode___Server
{
    public class Server
    {
        private const string HEADER_CALL = "NLC_CALL";
        private const string HEADER_RETURN = "NLC_RETURN";
        private const string HEADER_HANDSHAKE = "NLC_HANDSHAKE";
        private const string HEADER_MOVE = "NLC_MOVE";

        private RNGCryptoServiceProvider cRandom = new RNGCryptoServiceProvider(DateTime.Now.ToString());
        private Socket sSocket = null;

        /// <summary>
        /// Port for the server to listen on, changing this variable while the server is running will have no effect.
        /// </summary>
        public int iPort { get; set; }

        /// <summary>
        /// The number of maximum pending connections in queue.
        /// </summary>
        public int iBacklog { get; set; }

        /// <summary>
        /// Initializes the NLC Server. Credits to Killpot :^)
        /// </summary>
        /// <param name="port">Port for server to listen on.</param>
        public Server(int port = 1337, int maxBacklog = 5)
        {
            this.iPort = port;
            this.iBacklog = maxBacklog;

            sSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        /// Start server and begin listening for connections.
        /// </summary>
        public void Start()
        {
            sSocket.Bind(new IPEndPoint(IPAddress.Any, iPort));
            sSocket.Listen(iBacklog);
            sSocket.BeginAccept(AcceptCallback, null);
        }

        /// <summary>
        /// Stop listening for connections and close server.
        /// </summary>
        public void Stop()
        {
            if (sSocket == null)
                throw new Exception("Server is not running...");
            sSocket.Shutdown(SocketShutdown.Both);
            sSocket.Close();
        }

        private void AcceptCallback(IAsyncResult iAR)
        //Our method for accepting clients
        {
            Socket client = sSocket.EndAccept(iAR);
            Console.WriteLine("Client connected!");

            sClient sC = new sClient();
            sC.cSocket = client;
            sC.sCls = new SharedClass();
            sC.bSize = new byte[4];

            BeginEncrypting(ref sC);

            sC.cSocket.BeginReceive(sC.bSize, 0, sC.bSize.Length, SocketFlags.None, RetrieveCallback, sC);
            sSocket.BeginAccept(AcceptCallback, null);
        }

        private void RetrieveCallback(IAsyncResult iAR)
        // Handshake + Encryption is handled outside of this callback, so any message that makes it here is expected to be a method call/move.
        {
            sClient sC = (sClient)iAR.AsyncState;
            try
            {
                byte[] cBuffer = new byte[BitConverter.ToInt32(sC.bSize, 0)];
                sC.bSize = new byte[4];

                sC.cSocket.Receive(cBuffer);

                cBuffer = AES_Decrypt(cBuffer, sC.bKey);

                object[] oMsg = Formatter.Deserialize<object[]>(cBuffer);

                if (oMsg[0] as string != HEADER_CALL && oMsg[0] as string != HEADER_MOVE) // Check to make sure it's a method call/move
                    throw new Exception("Ahhh it's not a call or move, everyone run!");

                object[] oRet = new object[2];
                oRet[0] = HEADER_RETURN;

                MethodInfo[] mIA = typeof(SharedClass).GetMethods();

                bool mFound = false;

                foreach (MethodInfo mI in mIA)
                    if (mI.GetCustomAttributes(typeof(NLCCall), false).Any())
                        if (mI.Name == oMsg[1] as string)
                        {
                            mFound = true;
                            object[] mParams = new object[oMsg.Length - 2];
                            Array.Copy(oMsg, 2, mParams, 0, oMsg.Length - 2);
                            oRet[1] = mI.Invoke(sC.sCls, mParams);
                        }

                if (!mFound)
                    throw new Exception("Client called method that does not exist in Shared Class! (Did you remember the [NLCCall] Attribute?)");

                if (oRet[1] == null && oMsg[0] as string == HEADER_MOVE)
                    Console.WriteLine("Method {0} returned null! Possible mismatch?", oMsg[1] as string);

                Send(sC, oRet);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something broke: " + ex.Message);
            }
            finally
            {
                sC.cSocket.BeginReceive(sC.bSize, 0, sC.bSize.Length, SocketFlags.None, RetrieveCallback, sC);
            }
        }

        private void BeginEncrypting(ref sClient sC)
        {
            byte[] sSymKey;
            CngKey sCngKey = CngKey.Create(CngAlgorithm.ECDiffieHellmanP521);
            byte[] sPublic = sCngKey.Export(CngKeyBlobFormat.EccPublicBlob);

            Send(sC, HEADER_HANDSHAKE, sPublic);

            object[] oRecv = Receive(sC);

            if (oRecv[0] as string != HEADER_HANDSHAKE)
                sC.cSocket.Disconnect(true);

            byte[] cBuf = oRecv[1] as byte[];

            using (var sAlgo = new ECDiffieHellmanCng(sCngKey))
            using (CngKey cPubKey = CngKey.Import(cBuf, CngKeyBlobFormat.EccPublicBlob))
                sSymKey = sAlgo.DeriveKeyMaterial(cPubKey);

            sC.bKey = sSymKey;
        }

        private void Send(sClient sC, params object[] param)
        {
            byte[] bSend = Formatter.Serialize(param);
            if (sC.bKey != null)
                bSend = AES_Encrypt(bSend, sC.bKey);

            sC.cSocket.Send(BitConverter.GetBytes(bSend.Length));
            sC.cSocket.Send(bSend);
        }

        private object[] Receive(sClient sC)
        {
            byte[] bSize = new byte[4];
            sC.cSocket.Receive(bSize);

            byte[] sBuf = new byte[BitConverter.ToInt32(bSize, 0)];
            sC.cSocket.Receive(sBuf);
            if (sC.bKey != null)
                sBuf = AES_Decrypt(sBuf, sC.bKey);

            return Formatter.Deserialize<object[]>(sBuf);
        }

        public byte[] AES_Encrypt(byte[] bytesToBeEncrypted, byte[] passwordBytes)
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

    [AttributeUsage(AttributeTargets.Method)]
    public class NLCCall : Attribute { }

    public struct sClient
    {
        public Socket cSocket;
        public SharedClass sCls;
        public byte[] bKey;
        public byte[] bSize;
    }

    public static class Formatter
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