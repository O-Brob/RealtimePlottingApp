using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Peak.Can.Basic;
using Peak.Can.Basic.BackwardCompatibility;
using RealtimePlottingApp.Models;

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
        public event EventHandler<CanMessageReceivedEvent>? MessageReceived;
        // Maximum length of CAN data in bytes
        private const int MaxCanDataLength = 8;
        
        // Concurrent queue which holds received messages
        private readonly ConcurrentQueue<(uint canId, int length, byte[] buffer)> _messageQueue 
            = new ConcurrentQueue<(uint canId, int length, byte[] buffer)>();
        
        // Preallocated pool of byte arrays
        private readonly ConcurrentQueue<byte[]?> _bufferPool = new ConcurrentQueue<byte[]?>();
        
        // Stopwatch for timestamping messages.
        private readonly Stopwatch _stopwatch;
        private const int msInterval = 10; // Invoked resolution of timestamps.

        public PeakCanBus()
        {
            // Preallocate buffers to be reused as a receive pool
            for (int i = 0; i < 100; i++)
            {
                _bufferPool.Enqueue(new byte[MaxCanDataLength]);
            }
            
            _stopwatch = new Stopwatch();
        }
        
        public void Connect(string interfaceName, string? bitrate)
        {
            if (!TryGetPcanChannel(interfaceName, out _channel))
            {
                throw new Exception("Invalid PCAN interface name");
            }

            // Determine baudrate based on the provided bitrate string.
            TPCANBaudrate baudrate = TPCANBaudrate.PCAN_BAUD_100K; // default value
            if (!string.IsNullOrEmpty(bitrate))
            {
                switch (bitrate)
                {
                    case "1 MBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_1M;
                        break;
                    case "800 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_800K;
                        break;
                    case "500 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_500K;
                        break;
                    case "250 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_250K;
                        break;
                    case "125 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_125K;
                        break;
                    case "100 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_100K;
                        break;
                    case "95 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_95K;
                        break;
                    case "83 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_83K;
                        break;
                    case "50 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_50K;
                        break;
                    case "47 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_47K;
                        break;
                    case "33 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_33K;
                        break;
                    case "20 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_20K;
                        break;
                    case "10 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_10K;
                        break;
                    case "5 kBit/s":
                        baudrate = TPCANBaudrate.PCAN_BAUD_5K;
                        break;
                    default:
                        throw new Exception($"Unsupported bitrate: {bitrate}");
                }
            }

            // Initialize the channel with the determined baudrate.
            TPCANStatus status = PCANBasic.Initialize((ushort)_channel, baudrate);
            if (status != TPCANStatus.PCAN_ERROR_OK)
            {
                throw status switch
                {
                    // Common error, give more descriptive feedback
                    TPCANStatus.PCAN_ERROR_NODRIVER => new Exception(
                        "PCAN Initialization failed. The driver of the provided PCAN device " +
                        "could not be loaded. Ensure the PCAN driver is detected by your PC."),
                    // Common error, give more descr. feedback.
                    TPCANStatus.PCAN_ERROR_ILLHW => new Exception(
                        "PCAN Initialization failed. " +
                        "The PCAN device could not be loaded."),
                    _ => new Exception($"PCAN Initialization failed: {status}") // Fallback to "default" thrown message.
                };
            }
            
            // Start a fresh timer.
            _stopwatch.Restart();

            // Initialization has succeeded at this point, start receiving messages in background thread.
            _running = true;
            _receiveThread = new Thread(ReceiveMessages) { IsBackground = true };
            _receiveThread.Start();
            
            // Start a processing thread to handle messages from queue
            _processThread = new Thread(ProcessMessages) { IsBackground = true};
            _processThread.Start();
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
            
            _stopwatch.Reset(); // Stop & zero-set.
            PCANBasic.Uninitialize((ushort)_channel);
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
                            // Calculate timestamp as integer value, and divide by msInterval
                            // to get a specified resolution
                            uint timestamp = (uint)(_stopwatch.ElapsedMilliseconds / msInterval);
                            
                            MessageReceived?.Invoke(this, new CanMessageReceivedEvent(
                                msg.canId, 
                                msg.buffer.Take(msg.length).ToArray(),
                                timestamp)
                            );
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
