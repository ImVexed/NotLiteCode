using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace NotLiteCode___Server
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "NLC Server";
            Server server = new Server();
            server.Start();
            Process.GetCurrentProcess().WaitForExit();
        }
    }
}
