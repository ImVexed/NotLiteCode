using System;
using System.Diagnostics;

namespace NotLiteCode___Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.Title = "NLC Server";
            Server server = new Server();
            server.Start();
            Process.GetCurrentProcess().WaitForExit();
        }
    }
}