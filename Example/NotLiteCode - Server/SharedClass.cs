using NotLiteCode.Server;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NotLiteCode___Server
{
    public class SharedClass : IDisposable
    {
        [NLCCall("Pinocchio")]
        public string CombineTwoStringsAndReturn(string s1, string s2)
        {
            return "Magical server says, s1 + s2 = " + s1 + s2;
        }

        [NLCCall("JustATest", true)]
        public void Test(EndPoint Context)
        {
            Console.WriteLine($"Hey! {Context} invoked me!");
        }

        [NLCCall("ThroughputTest")]
        public async Task SpeedTest()
        {
            // Simulate asynchronous event
            await Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}