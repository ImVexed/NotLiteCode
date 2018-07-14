using NotLiteCode;
using NotLiteCode.Network;
using System;
using System.Diagnostics;

namespace NotLiteCode___Server
{
  internal class Program
  {
    public static NLCSocket Socket = new NLCSocket();
    public static Server<SharedClass> Server = new Server<SharedClass>(Socket);

    private static void Main(string[] args)
    {
      Console.Title = "NLC Server";
      Server.EnableDebug = true;
      Server.Start();

      Process.GetCurrentProcess().WaitForExit();
    }
  }
}