namespace WebSockets
{
    using System;

    /// <summary>Provides additional data for the <see cref="WebSocketTextClient.ErrorReceived"/> event.</summary>
    public sealed class SocketErrorEventArgs : EventArgs
    {
        /// <summary>Gets or sets the exception, that was raised.</summary>
        public Exception Exception { get; set; }

        /// <summary>Gets or sets the message that was passed with the error.</summary>
        public string Message { get; set; }
    }
}