using NotLiteCode;
using NotLiteCode.Network;
using System;
using System.Diagnostics;

namespace NotLiteCode___Client
{
  internal class Program
  {
    private static void Test() =>
      Client.RemoteCall("JustATest");

    private static string CombineTwoStringsAndReturn(string s1, string s2) =>
      Client.RemoteCall<string>("Pinocchio", s1, s2);

    private static void SpeedTest() => 
      Client.RemoteCall("ThroughputTest");
    

    private static Client Client = null;

    private static void Main(string[] args)
    {
      Console.Title = "NLC Client";

      var ClientSocket = new NLCSocket();

      ClientSocket.CompressorOptions.DisableCompression = true;
      ClientSocket.EncryptionOptions.DisableEncryption = true;

      Client = new Client(ClientSocket);

      Client.Connect("localhost", 1337);

      Test();

      Console.WriteLine(CombineTwoStringsAndReturn("I'm a ", "real boy!"));

      Stopwatch t = new Stopwatch();
      t.Start();

      int l = 0;

      
      while (t.ElapsedMilliseconds < 1000)
      {
        SpeedTest();
        l += 1;
      }

      t.Stop();

      Console.WriteLine("{0} calls in 1 second!", l);
      Client.Stop();
      Process.GetCurrentProcess().WaitForExit();
    }

    private void Log(string message, ConsoleColor color = ConsoleColor.Gray)
    {
      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.Write("[{0}] ", DateTime.Now.ToLongTimeString());
      Console.ForegroundColor = color;
      Console.Write("{0}{1}", message, Environment.NewLine);
      Console.ResetColor();
    }
  }
}