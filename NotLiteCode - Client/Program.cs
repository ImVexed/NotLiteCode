using NotLiteCode;
using NotLiteCode.Network;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NotLiteCode___Client
{
  internal class Program
  {
    private static async Task Test() =>
      await Client.RemoteCall("JustATest");

    private static async Task<string> CombineTwoStringsAndReturn(string s1, string s2) =>
      await Client.RemoteCall<string>("Pinocchio", s1, s2);

    private static async Task SpeedTest() =>
      await Client.RemoteCall("ThroughputTest");

    private static Client Client = null;

    private static void Main(string[] args)
    {
      Console.Title = "NLC Client";

      var ClientSocket = new NLCSocket();

      ClientSocket.CompressorOptions.DisableCompression = true;
      ClientSocket.EncryptorOptions.DisableEncryption = true;

      Client = new Client(ClientSocket, true);

      Client.Connect("localhost", 1337).Wait();

      Test().Wait();

      Console.WriteLine(CombineTwoStringsAndReturn("I'm a ", "real boy!").Result);

      Stopwatch t = new Stopwatch();
      t.Start();

      int l = 0;

      while (t.ElapsedMilliseconds < 1000)
        SpeedTest().ContinueWith((a) => l++);

      t.Stop();

      var finalCount = l;

      Console.WriteLine("{0} calls in 1 second!", finalCount);

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