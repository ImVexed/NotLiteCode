using System;
using System.Diagnostics;

namespace NotLiteCode
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.Title = "NLC Client";
            Client client = new Client();
            client.bDebugLog = true;
            client.Start();
            client.Test();

            Console.WriteLine(client.CombineTwoStringsAndReturn("I'm a ", "real boy!"));

            // Console will get spammed since we're calling the server 1k+ times in just a second, and it can effect speed.
            client.bDebugLog = false;
            int l = 0;
            Stopwatch t = new Stopwatch();
            t.Start();

            while (t.ElapsedMilliseconds < 1000)
            {
                client.SpeedTest();
                l += 1;
            }
            t.Stop();

            Console.WriteLine("{0} calls in 1 second!", l);
            client.Stop();
            Process.GetCurrentProcess().WaitForExit();
        }
    }
}