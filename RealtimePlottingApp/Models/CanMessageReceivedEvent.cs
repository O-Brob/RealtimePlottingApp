using System;

namespace RealtimePlottingApp.Models
{
    /// <summary>
    /// Event arguments for a received CAN message.
    /// </summary>
    public class CanMessageReceivedEvent : EventArgs
    {
        /// <summary>
        /// Gets the CAN message ID.
        /// </summary>
        public uint CanId { get; }

        /// <summary>
        /// Gets the data payload of the CAN message.
        /// </summary>
        public byte[] Data { get; }
        
        /// <summary>
        /// Gets the timestamp of when the message was received by the application.
        /// </summary>
        public uint Timestamp { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="CanMessageReceivedEvent"/> class.
        /// </summary>
        /// <param name="canId">The CAN message ID.</param>
        /// <param name="data">The received data payload.</param>
        /// <param name="timestamp">The timestamp to accompany this data</param>
        /// <exception cref="ArgumentNullException">Thrown if the data argument is null.</exception>
        public CanMessageReceivedEvent(uint canId, byte[] data, uint timestamp)
        {
            CanId = canId;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Timestamp = timestamp;
        }
    }
}