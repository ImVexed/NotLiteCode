[![Build Status](https://ci.appveyor.com/api/projects/status/github/ImVexed/notlitecode)](https://ci.appveyor.com/project/ImVexed/notlitecode)
# NotLiteCode
A simple hackable remote code hosting platform.

## What is?
NLC (Not Lite Code) is a simplified version of LiteCode by *DragonHunter*, which allows clients to execute code on a server as if they were calling a function that was being run locally(effectively [RMI](https://en.wikipedia.org/wiki/Distributed_object_communication)(Remote Method Invokation as opposed to non-OOP [RPC](https://en.wikipedia.org/wiki/Remote_procedure_call)(Remote Procedure Call)).
NLC intends to implement what was done in LiteCode but simplified and distilled down into 1 class (for the main logic atleast). 

## How does this differ from traditional RPC/RMI?
Traditionally RPC/RMI implements a stub interface and is tightly coupled. NLC however can function without a stub interface, and is loosly coupled. NLC also implements end to end encryption (alongside an Eliptic Curve Diffie-Hellman Handshake), DEFLATE compression, client isolation, and a stupidly simple implementation.  Each client that connects to the NLC server also get's a unique instance of the SharedClass, making multi-client implementations a breeze.

## Sample Implementation
### Server Code:
SharedClass.cs
```C#
[NLCCall("Pinocchio")] // Our target function on the server
public string CombineTwoStringsAndReturn(string s1, string s2)
{
  return "Magical server says, s1 + s2 = " + s1 + s2;
}
```
Program.cs
```C#
var server = new Server();
server.Start();
```
### Client Code:
Client.cs
```C#
public string CombineTwoStringsAndReturn(string s1, string s2)
    => RemoteCall<string>("Pinocchio", s1, s2);
```
Program.cs
```C#
Client client = new Client();
client.Start();

Console.WriteLine(client.CombineTwoStringsAndReturn("I'm a ", "real boy!")); // Returns "Magical server says, s1+ s2 = I'm a real boy!"
```
## Sample Outputs
<img src="http://image.prntscr.com/image/3dabba40de9643e18c2362a1e0e6f9d3.png" align="center" />
 
## Planned Features:
 - All features currently satisfied...
 
## Original
[LiteCode](https://github.com/AnguisCaptor/LiteCode) by *DragonHunter*
