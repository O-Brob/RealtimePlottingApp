using System;
using System.Collections.Concurrent;
using SocketCANSharp;
using System.Threading;
using System.Net.Sockets;
using System.Linq;
using RealtimePlottingApp.Models;
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
        // Thread for processing queued messages.
        private Thread? _processThread;
        // Flag to indicate if the CAN bus is running
        private volatile bool _running;
        // Event for handling received messages in order to notify the application.
        // It is essentially an observer pattern implementation for the CAN bus,
        // where the application can subscribe to the event to receive messages
        // and handle them using a lambda expression or a method.
        public event EventHandler<CanMessageReceivedEvent>? MessageReceived;
        // Maximum length of CAN data in bytes
        private const int MaxCanDataLength = 8;
        
        // Concurrent queue which holds received messages
        private readonly ConcurrentQueue<(uint canId, int length, byte[] buffer)> _messageQueue 
            = new ConcurrentQueue<(uint canId, int length, byte[] buffer)>();
        
        // Preallocated pool of byte arrays
        private readonly ConcurrentQueue<byte[]?> _bufferPool = new ConcurrentQueue<byte[]?>();

        public SocketCanBus()
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
                // Get the CanNetworkInterface by name
                CanNetworkInterface canInterface = CanNetworkInterface
                    .GetAllInterfaces(true)
                    .First(iface => iface.Name.Equals(interfaceName));

                // Ensure we found a valid interface
                if (canInterface == null)
                {
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
                
                // Start a processing thread to handle messages from queue
                _processThread = new Thread(ProcessMessages) { IsBackground = true};
                _processThread.Start();
                
                Console.WriteLine($"Successfully connected to {interfaceName}.");
                return true;
            }
            catch (SocketException e)
            {
                Console.WriteLine($"SocketCAN Connection Error: {e.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            // Tell receive thread to stop and close the socket
            _running = false;
            // Ensure the threads actually exists and is alive before trying to join
            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join();
            }

            if (_processThread != null && _processThread.IsAlive)
            {
                _processThread.Join();
            }
            _socket?.Close();
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
            while (_running)
            {
                try
                {
                    CanFrame frame = new CanFrame();
                    _socket?.Read(out frame);

                    if (frame.Length > 0 && frame.Data != null)
                    {
                        if (!_bufferPool.TryDequeue(out byte[]? buffer))
                        {
                            // Fallback if pool isn't enough. We prefer to not allocate new ones and use the
                            // pool to save recieve performance and leave less work for garbage collector.
                            buffer = new byte[MaxCanDataLength];
                        }
                        // Copy data into the borrowed buffer.
                        if (buffer == null) continue;
                        Array.Copy(frame.Data, buffer, frame.Length);

                        // Enqueue the message for processing.
                        _messageQueue.Enqueue((frame.CanId, frame.Length, buffer));
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine($"Error receiving message: {e.Message}");
                    break;
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
                            MessageReceived?.Invoke(this, new CanMessageReceivedEvent
                                (msg.canId, msg.buffer.Take(msg.length).ToArray()));
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
    }
}