namespace RealtimePlottingApp.Services.DataChannels
{
    /// <summary>
    /// A general interface for a data channel (UART, CAN, etc.).
    /// Implementations should handle connection and disconnection to a data source.
    /// </summary>
    public interface IDataChannel
    {
        /// <summary>
        /// Establish connection to the data channel.
        /// </summary>
        void Connect();

        /// <summary>
        /// Disconnect from the data channel.
        /// </summary>
        void Disconnect();
    }
}