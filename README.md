# WebSocketTextClient

Wrapper around System.Net.WebSockets.ClientWebSocket that provides event based interface to exchange text messages over Web Sockets and support cancellation.

Example:

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
