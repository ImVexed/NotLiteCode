using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

namespace NotLiteCode___Server
{
    public static class Headers
    {
        public const byte HEADER_CALL = 0x01;
        public const byte HEADER_RETURN = 0x02;
        public const byte HEADER_HANDSHAKE = 0x03;
        public const byte HEADER_MOVE = 0x04;
    }

    public class Server
    {
        private RNGCryptoServiceProvider cRandom = new RNGCryptoServiceProvider(DateTime.Now.ToString());
        private List<KeyValuePair<string, MethodInfo>> RemotingMethods = new List<KeyValuePair<string, MethodInfo>>();
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

            foreach (MethodInfo mI in typeof(SharedClass).GetMethods())
            {
                object[] oAttr = mI.GetCustomAttributes(typeof(NLCCall), false);
                if (oAttr.Any())
                    RemotingMethods.Add(new KeyValuePair<string, MethodInfo>((oAttr[0] as NLCCall).Identifier, mI));
            }
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

                object[] oMsg = BinaryFormatterSerializer.Deserialize(cBuffer);

                if (!oMsg[0].Equals(Headers.HEADER_CALL) && !oMsg[0].Equals(Headers.HEADER_MOVE)) // Check to make sure it's a method call/move
                    throw new Exception("Ahhh it's not a call or move, everyone run!");

                object[] oRet = new object[2];
                oRet[0] = Headers.HEADER_RETURN;

                MethodInfo[] mIA = typeof(SharedClass).GetMethods();

                MethodInfo mI = RemotingMethods.Find(x => x.Key == oMsg[1] as string).Value;

                if (mI == null)
                    throw new Exception("Client called method that does not exist in Shared Class! (Did you remember the [NLCCall] Attribute?)");

                oRet[1] = mI.Invoke(sC.sCls, oMsg.Skip(2).Take(oMsg.Length - 2).ToArray());

                if (oRet[1] == null && oMsg[0].Equals(Headers.HEADER_MOVE))
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

            Send(sC, Headers.HEADER_HANDSHAKE, sPublic);

            object[] oRecv = Receive(sC);

            if (!oRecv[0].Equals(Headers.HEADER_HANDSHAKE))
                sC.cSocket.Disconnect(true);

            byte[] cBuf = oRecv[1] as byte[];

            using (var sAlgo = new ECDiffieHellmanCng(sCngKey))
            using (CngKey cPubKey = CngKey.Import(cBuf, CngKeyBlobFormat.EccPublicBlob))
                sSymKey = sAlgo.DeriveKeyMaterial(cPubKey);

            sC.bKey = sSymKey;
        }

        private void Send(sClient sC, params object[] param)
        {
            byte[] bSend = BinaryFormatterSerializer.Serialize(param);
            if (sC.bKey != null)
                bSend = AES_Encrypt(bSend, sC.bKey);
            else
                bSend = Compress(bSend);

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
            else
                sBuf = Decompress(sBuf);

            return BinaryFormatterSerializer.Deserialize(sBuf);
        }

        public byte[] AES_Encrypt(byte[] bytesToBeEncrypted, byte[] passwordBytes)
        // Generic AES 256 class from StackOverFlow, modified to have random IV/Salt
        {
            object[] oOut = new object[3];
            byte[] counter = new byte[16];
            cRandom.GetBytes(counter);
            oOut[0] = counter;
            using (AesManaged AES = new AesManaged())
            {
                AES.KeySize = 256;
                AES.BlockSize = 128;
                var key = new Rfc2898DeriveBytes(passwordBytes, oOut[0] as byte[], 1000);
                AES.Mode = CipherMode.ECB;
                AES.Padding = PaddingMode.None;
                byte[] bHKey = key.GetBytes(AES.KeySize / 8);
                byte[] bHCounter = key.GetBytes(AES.BlockSize / 8);
                using (var cs = new CounterModeCryptoTransform(AES, bHKey, bHCounter))
                {
                    oOut[2] = cs.TransformFinalBlock(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);
                }
                oOut[1] = new HMACSHA256(bHKey).ComputeHash(oOut[2] as byte[]);
            }
            return Compress(BinaryFormatterSerializer.Serialize(oOut));
        }

        public byte[] AES_Decrypt(byte[] bytes, byte[] passwordBytes)
        {
            byte[] decryptedBytes = null;
            object[] oIn = BinaryFormatterSerializer.Deserialize(Decompress(bytes));
            using (AesManaged AES = new AesManaged())
            {
                AES.KeySize = 256;
                AES.BlockSize = 128;
                var key = new Rfc2898DeriveBytes(passwordBytes, oIn[0] as byte[], 1000);
                AES.Mode = CipherMode.ECB;
                AES.Padding = PaddingMode.None;
                byte[] bHKey = key.GetBytes(AES.KeySize / 8);
                byte[] bHCounter = key.GetBytes(AES.BlockSize / 8);
                if (!new HMACSHA256(bHKey).ComputeHash(oIn[2] as byte[]).SequenceEqual(oIn[1] as byte[]))
                    throw new Exception("Data has been modified! Oracle padding attack? Who cares! Run!");
                byte[] bytesToBeDecrypted = oIn[2] as byte[];
                using (var cs = new CounterModeCryptoTransform(AES, bHKey, bHCounter))
                {
                    decryptedBytes = cs.TransformFinalBlock(bytesToBeDecrypted, 0, bytesToBeDecrypted.Length);
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

    public class CounterModeCryptoTransform : ICryptoTransform
    {
        private readonly byte[] _counter;
        private readonly ICryptoTransform _counterEncryptor;
        private readonly Queue<byte> _xorMask = new Queue<byte>();
        private readonly SymmetricAlgorithm _symmetricAlgorithm;

        public CounterModeCryptoTransform(SymmetricAlgorithm symmetricAlgorithm, byte[] key, byte[] counter)
        {
            if (symmetricAlgorithm == null) throw new ArgumentNullException("symmetricAlgorithm");
            if (key == null) throw new ArgumentNullException("key");
            if (counter == null) throw new ArgumentNullException("counter");
            if (counter.Length != symmetricAlgorithm.BlockSize / 8)
                throw new ArgumentException(String.Format("Counter size must be same as block size (actual: {0}, expected: {1})",
                    counter.Length, symmetricAlgorithm.BlockSize / 8));

            _symmetricAlgorithm = symmetricAlgorithm;
            _counter = counter;

            var zeroIv = new byte[_symmetricAlgorithm.BlockSize / 8];
            _counterEncryptor = symmetricAlgorithm.CreateEncryptor(key, zeroIv);
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            var output = new byte[inputCount];
            TransformBlock(inputBuffer, inputOffset, inputCount, output, 0);
            return output;
        }

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            for (var i = 0; i < inputCount; i++)
            {
                if (NeedMoreXorMaskBytes()) EncryptCounterThenIncrement();

                var mask = _xorMask.Dequeue();
                outputBuffer[outputOffset + i] = (byte)(inputBuffer[inputOffset + i] ^ mask);
            }

            return inputCount;
        }

        private bool NeedMoreXorMaskBytes()
        {
            return _xorMask.Count() == 0;
        }

        private void EncryptCounterThenIncrement()
        {
            var counterModeBlock = new byte[_symmetricAlgorithm.BlockSize / 8];

            _counterEncryptor.TransformBlock(_counter, 0, _counter.Length, counterModeBlock, 0);
            IncrementCounter();

            foreach (var b in counterModeBlock)
            {
                _xorMask.Enqueue(b);
            }
        }

        private void IncrementCounter()
        {
            for (var i = _counter.Length - 1; i >= 0; i--)
            {
                if (++_counter[i] != 0)
                    break;
            }
        }

        public int InputBlockSize { get { return _symmetricAlgorithm.BlockSize / 8; } }
        public int OutputBlockSize { get { return _symmetricAlgorithm.BlockSize / 8; } }
        public bool CanTransformMultipleBlocks { get { return true; } }
        public bool CanReuseTransform { get { return false; } }

        public void Dispose()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class NLCCall : Attribute
    {
        public readonly string Identifier;

        public NLCCall(string Identifier)
        {
            this.Identifier = Identifier;
        }
    }

    public struct sClient
    {
        public Socket cSocket;
        public SharedClass sCls;
        public byte[] bKey;
        public byte[] bSize;
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