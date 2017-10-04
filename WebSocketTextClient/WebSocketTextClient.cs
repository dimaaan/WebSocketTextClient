using System;
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
        private readonly int InitialRecieveBufferSize;
        private readonly bool AutoIncreaseRecieveBuffer;

        /// <summary>
        /// Create new instance of WebSocketTextClient
        /// </summary>
        /// <param name="cancellationToken">Cancels pending send and receive operations</param>
        /// <param name="initialRecieveBufferSize">Socket initial receive buffer size in bytes</param>
        /// <param name="autoIncreaseRecieveBuffer">
        /// If true, receive buffer size will be doubled on overflow. 
        /// If false, unobserved InvalidOperationException will be thrown on overflow
        /// </param>
        public WebSocketTextClient(CancellationToken? cancellationToken = null, int initialRecieveBufferSize = 1024, bool autoIncreaseRecieveBuffer = true)
        {
            if (initialRecieveBufferSize <= 0)
                throw new ArgumentException("Receive buffer size should be greater than zero", nameof(initialRecieveBufferSize));

            CancellationToken = cancellationToken ?? CancellationToken.None;
            InitialRecieveBufferSize = initialRecieveBufferSize;
            AutoIncreaseRecieveBuffer = autoIncreaseRecieveBuffer;

            Socket = new ClientWebSocket();
            RecieveTask = new Task(RecieveLoop, CancellationToken);
        }

        /// <summary>
        /// Signals that response message fully received and ready to process
        /// </summary>
        public event Action<string> OnResponse;

        /// <summary>
        /// Asynchronously connects to WebSocket server and start receiving income messages in separate Task
        /// </summary>
        /// <param name="url">The URI of the WebSocket server to connect to</param>
        public async Task ConnectAsync(Uri url)
        {
            await Socket.ConnectAsync(url, CancellationToken);
            RecieveTask.Start();
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
            byte[] buffer = new byte[InitialRecieveBufferSize];
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

                        // check buffer overflow
                        if(!result.EndOfMessage && writeSegment.Count == 0)
                        {
                            if (AutoIncreaseRecieveBuffer)
                            {
                                Array.Resize(ref buffer, buffer.Length * 2);
                                writeSegment = new ArraySegment<byte>(buffer, writeSegment.Offset, buffer.Length - writeSegment.Offset);
                            }
                            else
                            {
                                throw new InvalidOperationException($"Socket receive buffer overflow. Buffer size = {buffer.Length}. Buffer auto-increase = {AutoIncreaseRecieveBuffer}");
                            }
                        }
                            
                    } while (!result.EndOfMessage);

                    var responce = Encoding.UTF8.GetString(buffer, 0, writeSegment.Offset);
                    OnResponse?.Invoke(responce);
                }
            }
            catch (OperationCanceledException)
            {
                // swallow cancel exception
            }
        }

        /// <summary>
        /// Close connection and stops message receiving Task
        /// </summary>
        public void Dispose()
        {
            Socket.Dispose();
            RecieveTask.Dispose();
        }
    }
}
