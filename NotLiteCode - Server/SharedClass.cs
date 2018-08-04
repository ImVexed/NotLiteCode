using NotLiteCode.Server;
using System;
using System.Net;

namespace NotLiteCode___Server
{
  public class SharedClass : IDisposable

  {
    [NLCCall("Pinocchio")] // Any method without the [NLCCall] attribute will not be executable by the client
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
    public void SpeedTest()
    { }

    public void Dispose()
    {
    }
  }
}