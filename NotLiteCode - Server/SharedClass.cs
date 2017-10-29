using System;

namespace NotLiteCode___Server
{
  public class SharedClass : IDisposable,
      Prototypes // Not required, but good for debugging's sake

  {
    [NLCCall("Pinocchio")] // Any method without the [NLCCall] attribute will not be executable by the client
    public string CombineTwoStringsAndReturn(string s1, string s2)
    {
      throw new Exception();
      return "Magical server says, s1 + s2 = " + s1 + s2;
    }

    [NLCCall("JustATest")]
    public void Test()
    {
      Console.WriteLine("Hey! The client invoked me!");
      Program.server.bDebugLog = false; // We're about to do a speed test after this message, and outputting all the info will effect the speed severely
    }

    [NLCCall("Sanic")]
    public void SpeedTest()
    { }

    public void Dispose()
    {
      // Make sure to remove all references to this instance inside here or else the class will not be properly disposed off, this combined with a large amount of clients, or large amounts of data being
      // stored in one instance of a SharedClass (Ex. a large string/byte array) will cause high memory usage.
    }
  }
}