using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSockets
{
    /// <summary>
    /// Wrapper around ClientWebSocket that provides suitable interface to exchange text messages over Web Sockets in event based way
    /// </summary>
    /// <seealso cref="System.Net.WebSockets.ClientWebSocket"/>
    public class WebSocketTextClient: IDisposable
    {
        private ClientWebSocket Socket;
        private CancellationToken CancellationToken;
        private Task RecieveTask;

        /// <summary>
        /// Create new instance of WebSocketTextClient
        /// </summary>
        /// <param name="cancellationToken">Cancels pending send and receive operations</param>
        public WebSocketTextClient(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            Socket = new ClientWebSocket();
            RecieveTask = new Task(RecieveLoop, cancellationToken);
        }

        /// <summary>Signals that response message fully received and ready to process.</summary>
        public event EventHandler<SocketMessageEventArgs> MessageReceived;

        /// <summary>Singals that the websocket received an error.</summary>
        public event EventHandler<SocketErrorEventArgs> ErrorReceived;

        /// <summary>Signals that the websocket was closed.</summary>
        public event EventHandler Closed;

        /// <summary>Signals that the socket has opened a connection.</summary>
        public event EventHandler Opened;

        /// <summary>
        /// Asynchronously connects to WebSocket server and start receiving income messages in separate Task
        /// </summary>
        /// <param name="url">The URI of the WebSocket server to connect to</param>
        public async Task ConnectAsync(Uri url)
        {
            await Socket.ConnectAsync(url, CancellationToken);
            RecieveTask.Start();

            await Task.Factory.FromAsync(
                this.SocketOpened.BeginInvoke,
                this.SocketOpened.EndInvoke,
                this,
                EventArgs.Empty,
                null);    
        }

        /// <summary>Adds custom request headers to the initial request.</summary>
        /// <param name="headers">A list of custom request headers.</param>
        public void AddHeaders(params KeyValuePair<string, string>[] headers)
        {
            foreach (var header in headers)
            {
                this.Socket.Options.SetRequestHeader(header.Key, header.Value);
            }
        }

        /// <summary>
        /// Asynchronously sends message to WebSocket server
        /// </summary>
        /// <param name="str">Message to send</param>
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

                    await Task.Factory.FromAsync(
                        this.SocketMessageReceived.BeginInvoke,
                        this.SocketMessageReceived.EndInvoke,
                        this,
                        new SocketMessageEventArgs { Message = responce },
                        null);
               }
            }
            catch (Exception ex)
            {
                await Task.Factory.FromAsync(
                    this.ErrorReceived.BeginInvoke,
                    this.ErrorReceived.EndInvoke,
                    this,
                    new SocketErrorEventArgs {Exception = ex, Message = string.Empty},
                    null);
            }

        }

        /// <summary>
        /// Close connection and stops message receiving Task
        /// </summary>
        public void Dispose()
        {
            Socket.Dispose();
            RecieveTask.Dispose();

            this.SocketClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    public sealed class SocketMessageEventArgs : EventArgs
    {
        public string Message { get; set; }
    }

    public sealed class SocketErrorEventArgs :EventArgs
    {
        public Exception Exception { get; set; }

        public string Message { get; set; }
    }
}
