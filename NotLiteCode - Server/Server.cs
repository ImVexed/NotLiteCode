using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;

namespace NotLiteCode___Server
{
  public static class Headers
  {
    public const byte HEADER_CALL = 0x01;
    public const byte HEADER_RETURN = 0x02;
    public const byte HEADER_HANDSHAKE = 0x03;
    public const byte HEADER_MOVE = 0x04;
    public const byte HEADER_ERROR = 0x05;
  }

  public class Server
  {
    private RNGCryptoServiceProvider cRandom = new RNGCryptoServiceProvider(DateTime.Now.ToString());
    private Dictionary<EndPoint, sClient> Clients = new Dictionary<EndPoint, sClient>();
    private Dictionary<string, MethodInfo> RemotingMethods = new Dictionary<string, MethodInfo>();
    private Socket sSocket = null;

    #region Variables

    /// <summary>
    /// Port for the server to listen on, changing this variable while the server is running will have no effect.
    /// </summary>
    public int iPort { get; set; }

    /// <summary>
    /// The number of maximum pending connections in queue.
    /// </summary>
    public int iBacklog { get; set; }

    /// <summary>
    /// If true, debugging information will be output to the console.
    /// </summary>
    public bool bDebugLog { get; set; } = false;

    #endregion Variables

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

      Log("Server started!", ConsoleColor.Green);

      sSocket.BeginAccept(AcceptCallback, null);

      foreach (MethodInfo mI in typeof(SharedClass).GetMethods())
      {
        var oAttr = mI.GetCustomAttributes(typeof(NLCCall), false);

        if (oAttr.Length > 0)
        {
          var thisAttr = oAttr[0] as NLCCall;

          if (RemotingMethods.Where(x => x.Key == thisAttr.Identifier).Any())
            throw new Exception("There are more than one function inside the SharedClass with the same Identifier!");

          Log($"Identifier {thisAttr.Identifier} MethodInfo link created...", ConsoleColor.Green);
          RemotingMethods.Add(thisAttr.Identifier, mI);
        }
      }
    }

    /// <summary>
    /// Stop listening for connections and close server.
    /// </summary>
    public void Stop()
    {
      if (sSocket == null)
        throw new Exception("Server is not running...");
      sSocket.Close();
    }

    private void AcceptCallback(IAsyncResult iAR)
    //Our method for accepting clients
    {
      var client = default(Socket);
      try
      {
        client = sSocket.EndAccept(iAR);
      }
      catch { return; } // This will happen when we shutdown the server

      Log($"Client connected from IP: {client.RemoteEndPoint}", ConsoleColor.Green, true);

      var sC = new sClient
      {
        cSocket = client,
        eCls = null,
        bSize = new byte[4],
        sClass = new SharedClass()
      };

      Clients.Add(sC.cSocket.RemoteEndPoint, sC);

      BeginEncrypting(ref sC);

      sC.cSocket.BeginReceive(sC.bSize, 0, sC.bSize.Length, SocketFlags.None, RetrieveCallback, sC.cSocket.RemoteEndPoint);

      sSocket.BeginAccept(AcceptCallback, null);
    }

    private void RetrieveCallback(IAsyncResult iAR)
    // Handshake + Encryption is handled outside of this callback, so any message that makes it here is expected to be a method call/move.
    {
      var sEP = iAR.AsyncState as EndPoint;
      var sC = Clients[sEP];
      try
      {
        SocketError sE;
        if (sC.cSocket.EndReceive(iAR, out sE) == 0 || sE != SocketError.Success)
        {
          Log($"Client IP: {sEP} has disconnected...", ConsoleColor.Yellow, true);
          sC.cSocket.Close();
          Clients[sEP].sClass.Dispose();
          Clients.Remove(sEP);
          // GC.Collect(0); For immediate disposal
          return;
        }

        var cBuffer = new byte[BitConverter.ToInt32(sC.bSize, 0)];
        sC.bSize = new byte[4];

        Log($"Receiving {cBuffer.Length} bytes...", ConsoleColor.Cyan);

        sC.cSocket.Receive(cBuffer);

        if (cBuffer.Length <= 0)
          throw new Exception("Received null buffer from client!");

        cBuffer = sC.eCls.AES_Decrypt(cBuffer);

        var oMsg = Encryption.BinaryFormatterSerializer.Deserialize(cBuffer);

        if (!oMsg[0].Equals(Headers.HEADER_CALL) && !oMsg[0].Equals(Headers.HEADER_MOVE)) // Check to make sure it's a method call/move
          throw new Exception("Ahhh it's not a call or move, everyone run!");

        var oRet = new object[2];
        oRet[0] = Headers.HEADER_RETURN;

        var mI = RemotingMethods[oMsg[1] as string];

        if (mI == null)
          throw new Exception("Client called method that does not exist in Shared Class! (Did you remember the [NLCCall] Attribute?)");
        try
        {
          oRet[1] = mI.Invoke(Clients[sEP].sClass, oMsg.Slice(2, oMsg.Length - 2));
        }
        catch
        {
          Log($"Client IP: {sEP} caused an exception invoking {oMsg[1]}");
          oRet[0] = Headers.HEADER_ERROR;
        }

        Log($"Client IP: {sEP} called Remote Identifier: {oMsg[1]}", ConsoleColor.Cyan);

        if (oRet[1] == null && oMsg[0].Equals(Headers.HEADER_MOVE) && !oRet[0].Equals(Headers.HEADER_ERROR))
          Console.WriteLine($"Method {oMsg[1]} returned null! Is this supposed to be a HEADER_CALL instead of HEADER_MOVE?");

        BlockingSend(sC, oRet);
        sC.cSocket.BeginReceive(sC.bSize, 0, sC.bSize.Length, SocketFlags.None, RetrieveCallback, sEP);
      }
      catch
      {
        Log($"Client IP: {sEP} has caused an exception...", ConsoleColor.Yellow, true);

        sC?.cSocket?.Close();
        Clients[sEP]?.sClass?.Dispose();
        Clients.Remove(sEP);
      }
    }

    private void BeginEncrypting(ref sClient sC)
    {
      byte[] sSymKey;
      CngKey sCngKey = CngKey.Create(CngAlgorithm.ECDiffieHellmanP521);
      byte[] sPublic = sCngKey.Export(CngKeyBlobFormat.EccPublicBlob);

      BlockingSend(sC, Headers.HEADER_HANDSHAKE, sPublic);

      object[] oRecv = BlockingReceive(sC);

      if (!oRecv[0].Equals(Headers.HEADER_HANDSHAKE))
        sC.cSocket.Disconnect(true);

      byte[] cBuf = oRecv[1] as byte[];

      using (var sAlgo = new ECDiffieHellmanCng(sCngKey))
      using (CngKey cPubKey = CngKey.Import(cBuf, CngKeyBlobFormat.EccPublicBlob))
        sSymKey = sAlgo.DeriveKeyMaterial(cPubKey);

      sC.eCls = new Encryption(sSymKey, HASH_STRENGTH.MEDIUM);
    }

    private void BlockingSend(sClient sC, params object[] param)
    {
      byte[] bSend = Encryption.BinaryFormatterSerializer.Serialize(param);
      if (sC.eCls != null)
        bSend = sC.eCls.AES_Encrypt(bSend);
      else
        bSend = Encryption.Compress(bSend);

      Log($"Sending {bSend.Length} bytes...", ConsoleColor.Cyan);

      sC.cSocket.Send(BitConverter.GetBytes(bSend.Length));
      sC.cSocket.Send(bSend);
    }

    private object[] BlockingReceive(sClient sC)
    {
      byte[] bSize = new byte[4];
      sC.cSocket.Receive(bSize);

      byte[] sBuf = new byte[BitConverter.ToInt32(bSize, 0)];
      int iReceived = 0;

      while (iReceived < sBuf.Length)
        iReceived += sC.cSocket.Receive(sBuf, iReceived, sBuf.Length - iReceived, SocketFlags.None);

      Log($"Receiving {sBuf.Length} bytes...", ConsoleColor.Cyan);

      if (sC.eCls != null)
        sBuf = sC.eCls.AES_Decrypt(sBuf);
      else
        sBuf = Encryption.Decompress(sBuf);

      return Encryption.BinaryFormatterSerializer.Deserialize(sBuf);
    }

    private void Log(string message, ConsoleColor color = ConsoleColor.Gray, bool force = false)
    {
      if (!bDebugLog && !force)
        return;

      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.Write("[{0}] ", DateTime.Now.ToLongTimeString());
      Console.ForegroundColor = color;
      Console.Write("{0}{1}", message, Environment.NewLine);
      Console.ResetColor();
    }
  }

  public static partial class Helpers
  {
    public static T[] Slice<T>(this T[] source, int start, int length)
    {
      var result = new T[length];
      Array.Copy(source, start, result, 0, length);
      return result;
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

  public class sClient
  {
    public Socket cSocket;
    public Encryption eCls;
    public SharedClass sClass;
    public byte[] bSize;
  }
}