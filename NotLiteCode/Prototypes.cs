namespace NotLiteCode
{
  // Define your remote functions in here, you could alternatively make a dll with just this inside and use it in place of 2 (possibly different) Prototype interfaces in both the client and server to insure consistency.
  // That would prevent any possible mismatches. However I didn't do that because this part really isn't even required, you could remove the Prototypes extension from both the server and client and nothing would change.
  public interface Prototypes
  {
    void Test();

    string CombineTwoStringsAndReturn(string s1, string s2);

    void SpeedTest();
  }
}