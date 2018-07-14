[![Build Status](https://ci.appveyor.com/api/projects/status/github/ImVexed/notlitecode?branch=master)](https://ci.appveyor.com/project/ImVexed/notlitecode?branch=master)

[![Code Factor](https://www.codefactor.io/repository/github/imvexed/notlitecode/badge)](https://www.codefactor.io/repository/github/imvexed/notlitecode)

[![GitHub license](https://img.shields.io/github/license/ImVexed/NotLiteCode.svg)](https://github.com/ImVexed/NotLiteCode/blob/master/LICENSE)

# NotLiteCode
A simple hackable remote code hosting platform.

## Update as of 7/14/2018
I've begun refactoring the codebase, expect much more modular code & a much better development experience. Expect slight instability as the codebase normalizes.

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
var socket = new NLCSocket();
var server = new Server<SharedClass>(socket);
server.Start();
```
### Client Code:
Program.cs
```C#
private static string CombineTwoStringsAndReturn(string s1, string s2) =>
      Client.RemoteCall<string>("Pinocchio", s1, s2);
      
var Socket = new NLCSocket();
Client = new Client(Socket);

Client.Connect("localhost", 1337);

Console.WriteLine(CombineTwoStringsAndReturn("I'm a ", "real boy!")); // Returns "Magical server says, s1 + s2 = I'm a real boy!"
```
## Sample Outputs
<img src="http://image.prntscr.com/image/3dabba40de9643e18c2362a1e0e6f9d3.png" align="center" />
 
## Planned Features:
 - NuGet Package
 - More modularization & abstraction to appropriate levels
 
## Original
[LiteCode](https://gitlab.com/Dergan/LiteCode) by *DragonHunter* 
