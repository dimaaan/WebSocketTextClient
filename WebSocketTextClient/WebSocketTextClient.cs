using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSockets
{
    /// <summary>
    /// Wrapper around ClientWebSocket that provides suitable interface to exchange text messages over Web Sockets
    /// </summary>
    /// <seealso cref="System.Net.WebSockets.ClientWebSocket"/>
    public class WebSocketTextClient: IDisposable
    {
        private ClientWebSocket Socket;
        private CancellationToken CancellationToken;
        private Task RecieveTask;
        private bool Disposed = false; // To detect redundant calls

        public WebSocketTextClient(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            Socket = new ClientWebSocket();
            RecieveTask = new Task(RecieveLoop, cancellationToken);
        }

        public event Action<string> OnResponse;

        public async Task ConnectAsync(Uri url)
        {
            await Socket.ConnectAsync(url, CancellationToken);
            RecieveTask.Start();
        }

        public Task SendAsync(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            return Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken);
        }

        private async void RecieveLoop()
        {
            byte[] buffer = new byte[1024];
            ArraySegment<byte> writeSegment;
            WebSocketReceiveResult result;

            try
            {
                while (true)
                {
                    writeSegment = new ArraySegment<byte>(buffer);
                    do
                    {
                        result = await Socket.ReceiveAsync(writeSegment, CancellationToken);
                        writeSegment = new ArraySegment<byte>(buffer, writeSegment.Offset + result.Count, writeSegment.Count - result.Count);
                    } while (!result.EndOfMessage);

                    var responce = Encoding.UTF8.GetString(buffer, 0, writeSegment.Offset);
                    OnResponse?.Invoke(responce);
                }
            }
            catch (AggregateException e) when (e.InnerException is TaskCanceledException)
            {
                // swallow cancel exception
            }
        }

        public void Dispose()
        {
            Socket.Dispose();
            RecieveTask.Dispose();
        }
    }
}
