using System;
using System.Net.Sockets;
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
        private Encryption eCls = null;

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
        /// If true, debugging information will be output to the console.
        /// </summary>
        public bool bDebugLog { get; set; } = false;

        #endregion Variables

        #region Prototypes

        // Override of Prototypes.Test
        public void Test()//                                           Identifier is defined in the server's Shared Class for that method
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
            Log("Successfully connected to server!", ConsoleColor.Green);
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

            object[] oRecv = BlockingReceive();
            if (!oRecv[0].Equals(Headers.HEADER_HANDSHAKE)) // Sanity check
                throw new Exception("Unexpected error");

            byte[] sBuf = oRecv[1] as byte[];

            using (var cAlgo = new ECDiffieHellmanCng(cCngKey))
            using (CngKey sPubKey = CngKey.Import(sBuf, CngKeyBlobFormat.EccPublicBlob))
                bKeyTemp = cAlgo.DeriveKeyMaterial(sPubKey);

            BlockingSend(Headers.HEADER_HANDSHAKE, cPublic);

            Log(String.Format("Handshake complete, key length: {0}", bKeyTemp.Length), ConsoleColor.Green);

            eCls = new Encryption(bKeyTemp);
        }

        private T RemoteCall<T>(string identifier, params object[] param)
        {
            object[] payload = new object[param.Length + 2]; // +2 for header & method name
            payload[0] = Headers.HEADER_MOVE;
            payload[1] = identifier;
            Array.Copy(param, 0, payload, 2, param.Length);
            Log(String.Format("Calling remote method: {0}", identifier), ConsoleColor.Cyan);
            BlockingSend(payload);
            object[] oRecv = BlockingReceive();

            if (!oRecv[0].Equals(Headers.HEADER_RETURN))
                throw new Exception("Unexpected error");

            return (T)oRecv[1];
        }

        private void RemoteCall(string identifier, params object[] param)
        {
            object[] payload = new object[param.Length + 2]; // +2 for header & method name
            payload[0] = Headers.HEADER_CALL;
            payload[1] = identifier;
            Array.Copy(param, 0, payload, 2, param.Length);
            Log(String.Format("Calling remote method: {0}", identifier), ConsoleColor.Cyan);
            BlockingSend(payload);
            object[] oRecv = BlockingReceive();

            if (!oRecv[0].Equals(Headers.HEADER_RETURN))
                throw new Exception("Unexpected error");
        }

        private object[] BlockingReceive()
        {
            byte[] bSize = new byte[4];
            cS.Receive(bSize);

            byte[] sBuf = new byte[BitConverter.ToInt32(bSize, 0)];
            cS.Receive(sBuf);

            if (sBuf.Length <= 0)
                throw new Exception("Invalid data length, did the server force disconnect you?");

            Log(String.Format("Receiving {0} bytes...", sBuf.Length), ConsoleColor.Cyan);

            if (eCls != null)
                sBuf = eCls.AES_Decrypt(sBuf);
            else
                sBuf = Encryption.Decompress(sBuf);

            return BinaryFormatterSerializer.Deserialize(sBuf);
        }

        private void BlockingSend(params object[] param)
        {
            byte[] bSend = BinaryFormatterSerializer.Serialize(param);

            if (eCls != null)
                bSend = eCls.AES_Encrypt(bSend);
            else
                bSend = Encryption.Compress(bSend);
            Log(String.Format("Sending {0} bytes...", bSend.Length), ConsoleColor.Cyan);
            cS.Send(BitConverter.GetBytes(bSend.Length)); // Send expected payload length, gets bytes of int representing size, will always be 4 bytes for Int32
            cS.Send(bSend);
        }

        private void Log(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            if (!bDebugLog)
                return;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[{0}] ", DateTime.Now.ToLongTimeString());
            Console.ForegroundColor = color;
            Console.Write("{0}{1}", message, Environment.NewLine);
            Console.ResetColor();
        }
    }
}