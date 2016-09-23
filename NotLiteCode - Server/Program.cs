using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NotLiteCode___Server
{
    internal class Program
    {
        public static Server server = new Server();

        private static void Main(string[] args)
        {
            Console.Title = "NLC Server";

            server.bDebugLog = true;
            server.Start();

            Process.GetCurrentProcess().WaitForExit();
        }
    }


}