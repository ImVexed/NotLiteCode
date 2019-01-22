using NotLiteCode.Client;
using NotLiteCode.Network;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NotLiteCode___Client
{
    internal class Program
    {
        #region Remote Methods

        // In here we have our prototypes for the methods that exist on the server

        private static async Task Test() =>
          await Client.RemoteCall("JustATest");

        private static async Task<string> CombineTwoStringsAndReturn(string s1, string s2) =>
          await Client.RemoteCall<string>("Pinocchio", s1, s2);

        private static async Task SpeedTest() =>
          await Client.RemoteCall("ThroughputTest");

        #endregion Remote Methods

        private static Client Client = null;

        private static void Main(string[] args)
        {
            Main().Wait();
        }

        private static async Task Main()
        {
            Console.Title = "NLC Client";

            // Create a socket with encryption enabled
            var ClientSocket = new NLCSocket(UseSSL: true, AllowInsecureCerts: true);

            Client = new Client(ClientSocket);

            Client.Connect("localhost", 1337);

            // Invoke our first remote method
            await Test();

            // Invoke a method that returns a string
            Console.WriteLine(await CombineTwoStringsAndReturn("I'm a ", "real boy!"));

            int l = 0;

            var t = Stopwatch.StartNew();

            // Execute a method as many times as we can in 1 second
            while (t.ElapsedMilliseconds < 1000)
            {
                await SpeedTest();
                l++;
            }

            t.Stop();

            Console.WriteLine("{0} calls in 1 second!", l);

            Client.Stop();
            Console.ReadLine();
        }
    }
}