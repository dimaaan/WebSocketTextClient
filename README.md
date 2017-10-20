# WebSocketTextClient

Wrapper around System.Net.WebSockets.ClientWebSocket that provides event based interface to exchange text messages over Web Sockets in event based way and support cancellation. Includes events for open/close, message received and error received.

## Target Platform: 
.NET Standard 2.0

## Install

#### Package Manager
`Install-Package WebSocketTextClient`

#### DotNetCore
`dotnet add package WebSocketTextClient`

#### Packet
`paket add WebSocketTextClient`

## Example

```csharp
using (var cts = new CancellationTokenSource())
using (var client = new WebSocketTextClient(cts.Token))
{
    client.MessageReceived = (sender, eventArgs) => Console.WriteLine(eventArgs.Message);

    await client.ConnectAsync(new Uri("ws://example.com"));
    await client.SendAsync("ping");
    await Task.Delay(5000);
}
```
