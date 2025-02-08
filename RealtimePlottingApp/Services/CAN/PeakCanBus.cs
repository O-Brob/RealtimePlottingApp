using System;
using System.Threading;
using Peak.Can.Basic;
using Peak.Can.Basic.BackwardCompatibility;

namespace RealtimePlottingApp.Services.CAN
{
    /// <summary>
    /// CAN bus implementation using PEAK-System PCAN-Basic API.
    /// This implementation is for Windows only.
    /// </summary>
    public class PeakCanBus : ICanBus
    {
        // PEAK CAN channel for communication
        private PcanChannel _channel;
        // Thread for receiving messages from the CAN bus to avoid blocking the main thread
        private Thread? _receiveThread;
        // Flag to indicate if the CAN bus is running
        private volatile bool _running;
        // Event for handling received messages in order to notify the application.
        // It is essentially an observer pattern implementation for the CAN bus,
        // where the application can subscribe to the event to receive messages.
        public event Action<uint, byte[]>? MessageReceived;
        // Maximum length of CAN data in bytes
        private const int MaxCanDataLength = 8;

        // Helper method to get the PEAK CAN channel based on the interface name.
        private static bool TryGetPcanChannel(string interfaceName, out PcanChannel channel)
        {
            // Ensure there is a value for channel
            channel = default;

            // Check that the Channel starts with PCAN-USB, case-insensitive.
            if (!interfaceName.StartsWith("PCAN-USB", StringComparison.OrdinalIgnoreCase))
                return false;

            // Extract number after "PCAN-USB"
            string numberPart = interfaceName.Substring(8);

            // There are 16 possible USB channels in peak's API:
            // So check that the number is in the interval [1..16]
            if (int.TryParse(numberPart, out int usbIndex) && usbIndex >= 1 && usbIndex <= 16)
            {
                // The channel should be assigned an enum such as "PcanChannel.Usb01"
                // So create the expected substring with 2 digits and parse it as an enum.
                channel = (PcanChannel)Enum.Parse(typeof(PcanChannel), $"Usb{usbIndex:D2}");
                return true;
            }

            return false;
        }


        public bool Connect(string interfaceName)
        {
            try
            {
                if (!TryGetPcanChannel(interfaceName, out _channel))
                {
                    // TODO: Might want something else than prints here eventually.
                    Console.WriteLine("Invalid CAN interface.");
                    return false;
                }

                // TODO: Probably want to be able to set the baudrate via GUI or initialization.
                TPCANStatus status = PCANBasic.Initialize((ushort)_channel, TPCANBaudrate.PCAN_BAUD_100K);
                if (status != TPCANStatus.PCAN_ERROR_OK)
                {
                    // TODO: Might want something else than prints here eventually.
                    Console.WriteLine($"PCAN Initialization failed: {status}");
                    return false;
                }

                // Initialization has succeeded at this point, start receiving messages in background thread.
                _running = true;
                _receiveThread = new Thread(ReceiveMessages) { IsBackground = true };
                _receiveThread.Start();
                // TODO: Might want something else than prints here eventually.
                Console.WriteLine("Successfully connected to PEAK CAN bus.");
                return true;
            }
            catch (Exception ex)
            {
                // TODO: Might want something else than prints here eventually.
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            _running = false;
            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join();
            }
            PCANBasic.Uninitialize((ushort)_channel);
            // TODO: Might want something else than prints here eventually.
            Console.WriteLine("Disconnected from PEAK CAN bus.");
        }

        public int SendMessage(uint canId, byte[] data)
        {
            if (data.Length > MaxCanDataLength)
                return 0;

            TPCANMsg message = new TPCANMsg
            {
                ID = canId,
                LEN = (byte)data.Length,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD, //11bit identifierr frame
                DATA = data
            };

            TPCANStatus status = PCANBasic.Write((ushort)_channel, ref message);
            if (status != TPCANStatus.PCAN_ERROR_OK)
            {
                // TODO: Might want something else than prints here eventually.
                Console.WriteLine($"Error sending message: {status}");
                return 0;
            }
            return data.Length;
        }

        private void ReceiveMessages()
        {
            while (_running)
            {
                TPCANMsg message;
                TPCANTimestamp timestamp;
                TPCANStatus status = PCANBasic.Read((ushort)_channel, out message, out timestamp);
                if (status == TPCANStatus.PCAN_ERROR_OK && message.LEN > 0)
                {
                    byte[] data = new byte[message.LEN];
                    Array.Copy(message.DATA, data, message.LEN);
                    //TODO: Send timestamp too? microsecond accuracy according to docs at best, check how it compares with linux implementation.
                    MessageReceived?.Invoke(message.ID, data);
                }
            }
        }
    }
}
