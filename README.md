# WebSocketTextClient

Wrapper around System.Net.WebSockets.ClientWebSocket that provides event based interface to exchange text messages over Web Sockets and support cancellation.

## Target Platform: 
.NET Standard 2.0

## Install

#### Package Manager
`Install-Package WebSocketTextClient`

#### DotNetCore
`dotnet add package WebSocketTextClient`

#### Packet
`paket add WebSocketTextClient --version 1.0.0`

## Example

```csharp
using (var cts = new CancellationTokenSource())
using (var client = new WebSocketTextClient(cts.Token))
{
    client.OnResponse = (text) => Console.WriteLine(text);

    await client.Connect(new Uri("ws://example.com"));
    await client.Send("ping");
    await Task.Delay(5000);
}
```
