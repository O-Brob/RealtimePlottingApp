using System;
using System.Diagnostics;
using SocketCANSharp;
using System.Threading;
using System.Net.Sockets;
using System.Linq;
using SocketCANSharp.Network;

namespace RealtimePlottingApp.Services.CAN
{
    /// <summary>
    /// CAN bus implementation using the SocketCAN-Sharp library.
    /// This implementation is for Linux only.
    /// </summary>
    /// <remarks>
    /// This implementation is based on the SocketCAN-Sharp library made by derek-will, which is a C# wrapper for the SocketCAN API.
    /// The SocketCAN API is a set of open-source CAN drivers for Linux, which allows applications to communicate with CAN devices.
    /// </remarks>
    public class SocketCanBus : ICanBus
    {
        // SocketCAN socket instance for communication
        private RawCanSocket? _socket;
        // Thread for receiving messages from the CAN bus to avoid blocking the main thread
        private Thread? _receiveThread;
        // Flag to indicate if the CAN bus is running
        private volatile bool _running;
        // Event for handling received messages in order to notify the application.
        // It is essentially an observer pattern implementation for the CAN bus,
        // where the application can subscribe to the event to receive messages
        // and handle them using a lambda expression or a method.
        public event Action<uint, byte[]>? MessageReceived;
        // Maximum length of CAN data in bytes
        private const int MaxCanDataLength = 8;
        // High precision system clock timer
        private readonly Stopwatch timestamp = new Stopwatch();

        public bool Connect(string interfaceName)
        {
            try
            {
                // Get the CanNetworkInterface by name
                var canInterface = CanNetworkInterface.GetAllInterfaces(true).First(iface => iface.Name.Equals(interfaceName));

                // Ensure we found a valid interface
                if (canInterface == null)
                {
                    // TODO: Might want something else than prints here eventually.
                    Console.WriteLine($"CAN interface {interfaceName} not found.");
                    return false;
                }

                // Initialize and bind the socket
                _socket = new RawCanSocket();
                _socket.Bind(canInterface);

                // Set the socket to a listening state, initialization is successful.
                _running = true;

                // Start the receiving thread in the background to read messages
                _receiveThread = new Thread(ReceiveMessages) { IsBackground = true };
                _receiveThread.Start();

                // TODO: Might want something else than prints here eventually.
                Console.WriteLine($"Successfully connected to {interfaceName}.");
                return true;
            }
            catch (SocketException e)
            {
                // TODO: Might want something else than prints here eventually.
                Console.WriteLine($"SocketCAN Connection Error: {e.Message}");
                return false;
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
            // Tell receive thread to stop and close the socket
            _running = false;
            // Ensure the thread is actually exists and is alive before trying to join
            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join();
            }
            _socket?.Close();
            timestamp.Stop(); // Stop timer too if it's running.
        }

        public int SendMessage(uint canId, byte[] data)
        {
            if (_socket == null || data.Length > MaxCanDataLength || !_socket.Connected)
                return 0;

            // Docs says "Classical frame format", assuming they mean "Standard 2.0A" with 11b identifier..
            var frame = new CanFrame
            {
                CanId = canId,
                Data = data,
                Length = (byte)data.Length
            };

            try
            {
                return _socket.Write(frame);
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Error sending message: {e.Message}");
                return 0;
            }
        }


        private void ReceiveMessages()
        {
            // Start a stopwatch using system clock ticks for high precision timing
            timestamp.Start();
            while (_running)
            {
                try
                {
                    CanFrame frame = new CanFrame();
                    _socket?.Read(out frame);

                    if (frame.Length > 0 && frame.Data != null)
                    {
                        // This should allow for a microsecond-accuracy timer using system clock.
                        // TODO: Probably want to decrease this to milliseconds anyways, can use timestamp.ElapsedMilliseconds then,
                        // the Peak version also supports milliseconds similarily. Would make for a uniform time measurement, albeit
                        // with a *slightly* higher accuracy for the windows peak driver as it uses peak timestamps rather than application stamps.
                        long timestampInMicroseconds = (timestamp.ElapsedTicks * 1_000_000L) / Stopwatch.Frequency;
                        
                        byte[] data = frame.Data.Take(frame.Length).ToArray();
                        MessageReceived?.Invoke(frame.CanId, data);
                    }
                }
                catch (SocketException e)
                {
                    // TODO: Might want something else than prints here eventually.
                    Console.WriteLine($"Error receiving message: {e.Message}");
                    break;
                }
            }
        }
    }
}