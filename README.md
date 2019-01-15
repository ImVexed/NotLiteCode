[![Build Status](https://ci.appveyor.com/api/projects/status/github/ImVexed/notlitecode?branch=master)](https://ci.appveyor.com/project/ImVexed/notlitecode?branch=master)
[![Code Factor](https://www.codefactor.io/repository/github/imvexed/notlitecode/badge)](https://www.codefactor.io/repository/github/imvexed/notlitecode)
[![GitHub license](https://img.shields.io/github/license/ImVexed/NotLiteCode.svg)](https://github.com/ImVexed/NotLiteCode/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/Nuget.Core.svg)](https://www.nuget.org/packages/NotLiteCode)

# NotLiteCode
A simple, hackable, remote code hosting platform.

## What is?
NLC (Not Lite Code) is a simplified version of LiteCode by *DragonHunter*, which provides native RPC and other useful features without any external dependencies. 

## How does this differ from traditional RPC/RMI?
Traditionally RPC/RMI implements a stub interface and is tightly coupled. NLC however can function without a stub interface by using `BinaryFormatter` to serialize & deserialize objects on the fly at runtime allowing it to be loosly coupled. NLC also allows communication over `SSLStream` for security.

## How is state handled?
NLC creates a unique instance for every client allowing you to keep stateful data alongside their functions in the `SharedClass`.

## Sample Implementation
### Server Code:
SharedClass.cs
```C#
[NLCCall("MagicNumber")]
public bool IsMagicNumber(int number)
{
  return number == 7;
}
```
Program.cs
```C#
server = new Server<SharedClass>();
server.Start();
```
### Client Code:
Program.cs
```C#
public static bool IsMagicNumber(int number) =>
      client.RemoteCall<bool>("MagicNumber", number);
      
client = new Client();

client.Connect("localhost", 1337);

Console.WriteLine(IsMagicNumber(-10)); // False
Console.WriteLine(IsMagicNumber(7));   // True
```
## Sample Outputs
<img src="http://image.prntscr.com/image/3dabba40de9643e18c2362a1e0e6f9d3.png" align="center" />
 
## Original
[LiteCode](https://gitlab.com/Dergan/LiteCode) by *DragonHunter* 
