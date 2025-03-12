using System;
using System.Collections.Concurrent;
using System.Linq;
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
        // Thread for processing queued messages
        private Thread? _processThread;
        // Flag to indicate if the CAN bus is running
        private volatile bool _running;
        // Event for handling received messages in order to notify the application.
        // It is essentially an observer pattern implementation for the CAN bus,
        // where the application can subscribe to the event to receive messages.
        public event Action<uint, byte[]>? MessageReceived;
        // Maximum length of CAN data in bytes
        private const int MaxCanDataLength = 8;
        
        // Concurrent queue which holds received messages
        private readonly ConcurrentQueue<(uint canId, int length, byte[] buffer)> _messageQueue 
            = new ConcurrentQueue<(uint canId, int length, byte[] buffer)>();
        
        // Preallocated pool of byte arrays
        private readonly ConcurrentQueue<byte[]?> _bufferPool = new ConcurrentQueue<byte[]?>();

        public PeakCanBus()
        {
            // Preallocate buffers to be reused as a receive pool
            for (int i = 0; i < 100; i++)
            {
                _bufferPool.Enqueue(new byte[MaxCanDataLength]);
            }
        }
        
        public bool Connect(string interfaceName)
        {
            try
            {
                if (!TryGetPcanChannel(interfaceName, out _channel))
                {
                    Console.WriteLine("Invalid CAN interface.");
                    return false;
                }

                // TODO: Probably want to be able to set the baudrate via GUI or initialization.
                TPCANStatus status = PCANBasic.Initialize((ushort)_channel, TPCANBaudrate.PCAN_BAUD_100K);
                if (status != TPCANStatus.PCAN_ERROR_OK)
                {
                    Console.WriteLine($"PCAN Initialization failed: {status}");
                    return false;
                }

                // Initialization has succeeded at this point, start receiving messages in background thread.
                _running = true;
                _receiveThread = new Thread(ReceiveMessages) { IsBackground = true };
                _receiveThread.Start();
                
                // Start a processing thread to handle messages from queue
                _processThread = new Thread(ProcessMessages) { IsBackground = true};
                _processThread.Start();
                
                Console.WriteLine("Successfully connected to PEAK CAN bus.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            _running = false;
            
            // Check that the threads are not null & running before joining. 
            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join();
            }
            if (_processThread != null && _processThread.IsAlive)
            {
                _processThread.Join();
            }
            
            PCANBasic.Uninitialize((ushort)_channel);
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
                TPCANStatus status = PCANBasic.Read((ushort)_channel, out message);
                if (status == TPCANStatus.PCAN_ERROR_OK && message.LEN > 0)
                {
                    if (!_bufferPool.TryDequeue(out byte[]? buffer))
                    {
                        // Fallback if pool isn't enough. We prefer to not allocate new ones and use the
                        // pool to save recieve performance and leave less work for garbage collector.
                        buffer = new byte[MaxCanDataLength];
                    }
                    // Copy data into the borrowed buffer.
                    if (buffer == null) continue;
                    Array.Copy(message.DATA, buffer, message.LEN);

                    // Enqueue the message for processing.
                    _messageQueue.Enqueue((message.ID, message.LEN, buffer));
                }
            }
        }
        
        private void ProcessMessages()
        {
            while (_running)
            {
                try
                {
                    
                    while (_messageQueue.TryDequeue(out var msg))
                    {
                        try
                        {
                            MessageReceived?.Invoke(msg.canId, msg.buffer.Take(msg.length).ToArray());
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in MessageReceived handler: {ex.Message}");
                        }
                        _bufferPool.Enqueue(msg.buffer);
                    }
                    
                    // Sleep shortly to reduce CPU usage if no messages are in the queue.
                    if (_messageQueue.IsEmpty)
                    {
                        Thread.Sleep(1);
                    }
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error processing messages: {e.Message}");
                }
            }
        }
        
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
    }
}
