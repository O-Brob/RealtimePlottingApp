namespace RealtimePlottingApp.Services.CAN
{
    /// <summary>
    /// Interface for a CAN bus.
    /// It defines the methods and events that a CAN bus implementation should provide.
    /// This interface is used to abstract different CAN-bus implementations from the application logic.
    /// </summary>
    public interface ICanBus
    {
        // Connect to the CAN bus at the specified interface  
        bool Connect(string interfaceName);

        // Disconnect from the CAN bus
        void Disconnect();

        // Send a message on the CAN bus.
        // Returns the number of bytes sent.
        int SendMessage(uint canId, byte[] data);
        
        // Receive a message from the CAN bus and invoke the MessageReceived event.
        event System.Action<uint, byte[]>? MessageReceived;
    }
}