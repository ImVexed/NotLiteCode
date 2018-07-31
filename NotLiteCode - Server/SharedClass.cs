using NotLiteCode.Server;
using System;

namespace NotLiteCode___Server
{
  public class SharedClass : IDisposable

  {
    [NLCCall("Pinocchio")] // Any method without the [NLCCall] attribute will not be executable by the client
    public string CombineTwoStringsAndReturn(string s1, string s2)
    {
      return "Magical server says, s1 + s2 = " + s1 + s2;
    }

    [NLCCall("JustATest")]
    public void Test()
    {
      Console.WriteLine("Hey! The client invoked me!");
    }

    [NLCCall("ThroughputTest")]
    public void SpeedTest()
    { }

    public void Dispose()
    {
    }
  }
}