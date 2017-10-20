namespace WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Wrapper around ClientWebSocket that provides suitable interface to exchange text messages over Web Sockets in event based way.</summary>
    /// <seealso cref="System.Net.WebSockets.ClientWebSocket"/>
    public sealed class WebSocketTextClient : IDisposable
    {
        private readonly ClientWebSocket socket;
        private readonly int initialRecieveBufferSize;
        private readonly bool autoIncreaseRecieveBuffer;

        private CancellationTokenSource tokenSource;
        private Task recieveTask;

        /// <summary>Initializes a new instance of the <see cref="WebSocketTextClient"/> class.</summary>
        /// <param name="initialRecieveBufferSize">The initial buffer size for incoming messages in bytes.</param>
        /// <param name="autoIncreaseRecieveBuffer">True to double the buffer on overflow. Otherwise an <see cref="InvalidOperationException"/> will be thrown.</param>
        public WebSocketTextClient(int initialRecieveBufferSize = 1024, bool autoIncreaseRecieveBuffer = true)
        {
            if (initialRecieveBufferSize <= 0)
            {
                throw new ArgumentException("Receive buffer size should be greater than zero", nameof(initialRecieveBufferSize));
            }
            
            this.initialRecieveBufferSize = initialRecieveBufferSize;
            this.autoIncreaseRecieveBuffer = autoIncreaseRecieveBuffer;

            this.socket = new ClientWebSocket();
        }

        /// <summary>Signals that response message fully received and ready to process.</summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>Signals that the websocket received an error.</summary>
        public event EventHandler<SocketErrorEventArgs> ErrorReceived;

        /// <summary>Signals that the websocket was closed.</summary>
        public event EventHandler Closed;

        /// <summary>Signals that the socket has opened a connection.</summary>
        public event EventHandler Opened;

        /// <summary>Asynchronously connects to WebSocket server and start receiving income messages in separate Task.</summary>
        /// <param name="url">The <see cref="Uri"/> of the WebSocket server to connect to.</param>
        /// <param name="cancellationToken">The token used to close the socket connection.</param>
        public async Task ConnectAsync(Uri url, CancellationToken cancellationToken)
        {
            // Create a new token source, since we can't reuse an existing and cancelled one.
            var internalTokenSource = new CancellationTokenSource();
            this.tokenSource = cancellationToken != CancellationToken.None
                ? CancellationTokenSource.CreateLinkedTokenSource(internalTokenSource.Token, cancellationToken)
                : internalTokenSource;
            
            // Register the disconnect method as a fire and forget method to run when the user requests cancellation
            // Don't pass in the cancellation token from the token source, since the token is cancelling the request.
            this.tokenSource.Token.Register(() => Task.Run(this.DisconnectAsync));

            // Store the receive task with the new cancellation token.
            this.recieveTask = this.RecieveLoopAsync(this.tokenSource.Token);

            // Open the connection and raise the opened event.
            await socket.ConnectAsync(url, this.tokenSource.Token);
            this.recieveTask.Start();
            this.Opened?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Disconnects the WebSocket gracefully from the server.</summary>
        public async Task DisconnectAsync()
        {
            await this.socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed the connection", CancellationToken.None);

            // Check if cancellation is already requested, 
            // e.g. the user requested cancellation from outside the class.
            if (!this.tokenSource.IsCancellationRequested)
            {
                this.tokenSource.Cancel();
            }

            this.Closed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Adds custom request headers to the initial request.</summary>
        /// <param name="headers">A list of custom request headers.</param>
        public void AddHeaders(params KeyValuePair<string, string>[] headers)
        {
            foreach (var header in headers)
            {
                this.socket.Options.SetRequestHeader(header.Key, header.Value);
            }
        }

        /// <summary>Asynchronously sends message to WebSocket server</summary>
        /// <param name="str">Message to send</param>
        public Task SendAsync(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, tokenSource.Token);
        }

        private async Task RecieveLoopAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[initialRecieveBufferSize];

            try
            {
                while (true)
                {
                    var writeSegment = new ArraySegment<byte>(buffer);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(writeSegment, cancellationToken);
                        writeSegment = new ArraySegment<byte>(buffer, writeSegment.Offset + result.Count, writeSegment.Count - result.Count);

                        // check buffer overflow
                        if (!result.EndOfMessage && writeSegment.Count == 0)
                        {
                            if (autoIncreaseRecieveBuffer)
                            {
                                Array.Resize(ref buffer, buffer.Length * 2);
                                writeSegment = new ArraySegment<byte>(buffer, writeSegment.Offset, buffer.Length - writeSegment.Offset);
                            }
                            else
                            {
                                throw new InvalidOperationException($"Socket receive buffer overflow. Buffer size = {buffer.Length}. Buffer auto-increase = {autoIncreaseRecieveBuffer}");
                            }
                        }

                    } while (!result.EndOfMessage);

                    var responce = Encoding.UTF8.GetString(buffer, 0, writeSegment.Offset);

                    this.MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Message = responce });
                }
            }
            catch (Exception ex)
            {
                this.ErrorReceived?.Invoke(this, new SocketErrorEventArgs { Exception = ex });
            }
        }

        /// <summary>Close connection and stops the message receiving Task.</summary>
        /// <remarks>
        /// The dispose method only disposes the unmanaged resorces and does not close the underlying connection or stops the long running tasks gracefully.
        /// Before this object is disposed the <see cref="DisconnectAsync"/> should be called.
        /// </remarks>
        public void Dispose()
        {
            socket.Dispose();
            recieveTask.Dispose();
        }
    }
}
