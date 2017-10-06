namespace WebSockets
{
    using System;

    /// <summary> Provides additional data for the <see cref="WebSocketTextClient.MessageReceived"/> event.</summary>
    public sealed class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>Gets or sets the message that was received.</summary>
        public string Message { get; set; }
    }
}