using NotLiteCode.Server;
using NotLiteCode.Network;
using System;
using System.Diagnostics;
using System.Threading;

namespace NotLiteCode___Server
{
  internal class Program
  {
    private static void Main(string[] args)
    {
      Console.Title = "NLC Server";

      var ServerSocket = new NLCSocket();

      ServerSocket.CompressorOptions.DisableCompression = true;
      ServerSocket.EncryptionOptions.DisableEncryption = true;

      var Server = new Server<SharedClass>(ServerSocket);

      Server.OnServerClientConnected += (x, y) => Log($"Client {y.Client} connected!", ConsoleColor.Green);
      Server.OnServerClientDisconnected += (x, y) => Log($"Client {y.Client} disconnected!", ConsoleColor.Yellow);
      Server.OnServerMethodInvoked += (x, y) => Log($"Client {y.Client} {(y.WasErroneous ? "failed to invoke" : "invoked")} {y.Identifier}", y.WasErroneous ? ConsoleColor.Yellow : ConsoleColor.Cyan);
      Server.OnServerExceptionOccurred += (x, y) => Log($"Exception Occured! {y.Exception}", ConsoleColor.Red);

      Server.Start();

      Process.GetCurrentProcess().WaitForExit();
    }

    static readonly object WriteLock = new object();

    private static void Log(string message, ConsoleColor color)
    {
      lock (WriteLock)
      {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[{0}] ", DateTime.Now.ToLongTimeString());
        Console.ForegroundColor = color;
        Console.Write("{0}{1}", message, Environment.NewLine);
        Console.ResetColor();
      }
    }
  }
}