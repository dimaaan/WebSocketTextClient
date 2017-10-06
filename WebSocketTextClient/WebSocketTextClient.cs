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
    public class WebSocketTextClient : IDisposable
    {
        private readonly ClientWebSocket socket;
        private readonly CancellationToken cancellationToken;
        private readonly Task recieveTask;
        private readonly int initialRecieveBufferSize;
        private readonly bool autoIncreaseRecieveBuffer;

        /// <summary>Create new instance of WebSocketTextClient.</summary>
        /// <param name="cancellationToken">Cancels pending send and receive operations</param>
        /// <param name="initialRecieveBufferSize">Socket initial receive buffer size in bytes</param>
        /// <param name="autoIncreaseRecieveBuffer">
        /// If true, receive buffer size will be doubled on overflow. 
        /// If false, unobserved InvalidOperationException will be thrown on overflow
        /// </param>
        public WebSocketTextClient(CancellationToken? cancellationToken = null, int initialRecieveBufferSize = 1024, bool autoIncreaseRecieveBuffer = true)
        {
            socket = new ClientWebSocket();
            this.cancellationToken = cancellationToken ?? CancellationToken.None;
            

            recieveTask = new Task(RecieveLoop, this.cancellationToken);

            if (initialRecieveBufferSize <= 0)
            {
                throw new ArgumentException("Receive buffer size should be greater than zero", nameof(initialRecieveBufferSize));
            }

            this.initialRecieveBufferSize = initialRecieveBufferSize;
            this.autoIncreaseRecieveBuffer = autoIncreaseRecieveBuffer;
        }

        /// <summary>Signals that response message fully received and ready to process.</summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>Singals that the websocket received an error.</summary>
        public event EventHandler<SocketErrorEventArgs> ErrorReceived;

        /// <summary>Signals that the websocket was closed.</summary>
        public event EventHandler Closed;

        /// <summary>Signals that the socket has opened a connection.</summary>
        public event EventHandler Opened;

        /// <summary>Asynchronously connects to WebSocket server and start receiving income messages in separate Task.</summary>
        /// <param name="url">The <see cref="Uri"/> of the WebSocket server to connect to.</param>
        public async Task ConnectAsync(Uri url)
        {
            await socket.ConnectAsync(url, cancellationToken);
            recieveTask.Start();

            if (this.Opened != null)
            {
                await Task.Factory.FromAsync(
                    this.Opened.BeginInvoke,
                    this.Opened.EndInvoke,
                    this,
                    EventArgs.Empty,
                    null);
            }
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
            return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async void RecieveLoop()
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

                    if (this.MessageReceived != null)
                    {
                        await Task.Factory.FromAsync(
                            this.MessageReceived.BeginInvoke,
                            this.MessageReceived.EndInvoke,
                            this,
                            new MessageReceivedEventArgs { Message = responce },
                            null);
                    }
                }
            }
            catch (Exception ex)
            {
                if (this.ErrorReceived != null)
                {
                    await Task.Factory.FromAsync(
                        this.ErrorReceived.BeginInvoke,
                        this.ErrorReceived.EndInvoke,
                        this,
                        new SocketErrorEventArgs { Exception = ex, Message = string.Empty },
                        null);
                }
            }
        }

        /// <summary>Close connection and stops the message receiving Task.</summary>
        public void Dispose()
        {
            socket.Dispose();
            recieveTask.Dispose();

            this.Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
