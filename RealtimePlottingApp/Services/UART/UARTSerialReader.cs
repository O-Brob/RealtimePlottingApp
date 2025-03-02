using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using RealtimePlottingApp.Models;

namespace RealtimePlottingApp.Services.UART;

public class UARTSerialReader : ISerialReader
{
    // Instance variables
    private SerialPort? _serialPort; // Initialized in StartSerial.
    private bool _isReading = false;
    private byte[] _readBuffer = [];
    private int _dataPayloadBytes = 0;
    private int _packageSize = 0;
    private readonly List<byte> _receiveBuffer = []; // To be used with locking, ensure threadsafe.
    
    //----- ISerialReader API events -----//
    public event EventHandler<TimestampedDataReceivedEvent>? TimestampedDataReceived;
    
    //----- Constructor -----//
    public UARTSerialReader()
    {

    }
    
    //----- ISerialReader API methods -----//
    public void StartSerial(string comPort, int baudRate, UARTDataPayloadSize payloadDataSize)
    {
        if (_isReading)
            return;
        
        // Ensures the stream is stopped if you force shut down the application.
        // Makes it so that if you force shut down the application as it's going, you won't be
        // connecting to an already ongoing uart stream later.
        AppDomain.CurrentDomain.ProcessExit -= ForceStopCleanup; // Unregister if registered
        Console.CancelKeyPress -= ForceStopCleanup;
        AppDomain.CurrentDomain.ProcessExit += ForceStopCleanup; // Register or re-register
        Console.CancelKeyPress += ForceStopCleanup;

        _dataPayloadBytes = payloadDataSize switch
        {
            UARTDataPayloadSize.UART_PAYLOAD_8 => 1,
            UARTDataPayloadSize.UART_PAYLOAD_16 => 2,
            UARTDataPayloadSize.UART_PAYLOAD_32 => 4,
            _ => 1 // Assume 8bit data if payloadDataSize was somehow messed up
        };

        _packageSize = _dataPayloadBytes + 2; // Package size including timestamp (16bits)
        try
        {
            // Create the serial port with manually set buffer sizes (increasing read buffer) and open the port. 
            _serialPort = new SerialPort(comPort, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadBufferSize = 4096 * 2,
                WriteBufferSize = 2048,
                WriteTimeout = 500,
                // DtrEnable = true, // Enable if needed
            };

            // Linux's termios system (or bad optimizations within the SerialPort library which prevents consistent interactions with it) seems to
            // have issues where the configuration of the baudrate to a previously set value does not result in the configuration
            // being applied immediately. By setting the configuration to a "dummy value" before the real configuration,
            // the config seems to be applied immediately, preventing any occassional issues with starting reads for Linux.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _serialPort.BaudRate = 1;
                _serialPort.Open();
                _serialPort.Close();
                _serialPort.BaudRate = baudRate;
            }

            _serialPort.Open();
        }
        catch (Exception e)
        {
            Console.WriteLine("Something went wrong while opening serial port " + comPort + ": " + e.Message);
            throw;
        }
        
        // Prepare buffers for async reads, start the read loop.
        _readBuffer = new byte[4096];
        _isReading = true;
        StartReadingLoop();
        
        // Tell UART we're ready to receive (start message)
        try
        {
            _serialPort.Write([(byte)'S'], 0, 1);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error while writing start byte to serial port " + comPort + ": " + e.Message);
            throw;
        }
    }

    public void StopSerial()
    {
        if (!_isReading)
            return;

        try
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Write([(byte)'R'],0,1);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error sending stop byte: " + e.Message);
        }
        
        _isReading = false;
        
        // Close serial port
        try
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error closing port" + e.Message);
        }
    }
    
    //----- Private helper methods -----//
    // Async read loop using BaseStream, as normal high-level use of SerialPort library
    // seems to have issues at high baudrates, according to blog post by Ben Voigt, May 7 2014.
    // Implementation is therefore inspired by his solution: https://sparxeng.com/blog/software/must-use-net-system-io-ports-serialport
    private void StartReadingLoop()
    {
        Action? kickoffRead = null;
        kickoffRead = () =>
        {
            if (!_isReading || _serialPort == null || !_serialPort.IsOpen)
                return;

            try
            {
                    // BeginRead: "Begins an asynchronous read operation." --> this creates the asynchronicity
                    _serialPort.BaseStream.BeginRead(_readBuffer, 0, _readBuffer.Length, ar =>
                    {
                        // Ensure reading is still valid before the async reads as well
                        if (!_isReading || _serialPort == null || !_serialPort.IsOpen)
                            return;
                        
                        try
                        {
                            int actualLength = _serialPort.BaseStream.EndRead(ar);
                            if (actualLength > 0)
                            {
                                // Append the newly read bytes to our internal buffer.
                                lock (_receiveBuffer)
                                {
                                    for (int i = 0; i < actualLength; i++)
                                    {
                                        _receiveBuffer.Add(_readBuffer[i]);
                                    }
                                }

                                // Process complete packages.
                                ProcessReceiveBuffer();
                            }
                        }
                        catch (IOException e)
                        {
                            Console.WriteLine("I/O Exception during read: " + e.Message);
                            StopSerial();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Unexpected exception during read: " + e.Message);
                            StopSerial();
                        }

                        // Kick off a new async read of BaseStream before terminating
                        if (_isReading)
                        {
                            kickoffRead?.Invoke();
                        }
                    }, null);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error initiating BeginRead: " + e.Message);
                StopSerial();
            }
        };

        kickoffRead();
    }

    private void ProcessReceiveBuffer()
    {
        List<UARTTimestampedData> packages = new List<UARTTimestampedData>();

        lock (_receiveBuffer)
        {
            // While there is at least one complete package available...
            while (_receiveBuffer.Count >= _packageSize)
            {
                // Extract the package bytes.
                byte[] packageBytes = _receiveBuffer.GetRange(0, _packageSize).ToArray();

                // Remove the processed bytes from the buffer.
                _receiveBuffer.RemoveRange(0, _packageSize);
                
                // Extract payload bytes (first _dataPayloadBytes bytes)
                byte[] payloadBytes = new byte[_dataPayloadBytes];
                Array.Copy(packageBytes, 0, payloadBytes, 0, _dataPayloadBytes);
                // Data is transmitted in big-endian order.
                // If the system is little-endian, reverse the payload bytes.
                if (BitConverter.IsLittleEndian && _dataPayloadBytes > 1)
                {
                    Array.Reverse(payloadBytes);
                }

                // Use switch-case to convert payload bytes to an unsigned integer.
                uint dataValue = _dataPayloadBytes switch
                {
                    1 => payloadBytes[0],
                    2 => BitConverter.ToUInt16(payloadBytes, 0),
                    4 => BitConverter.ToUInt32(payloadBytes, 0),
                    _ => 0
                };

                // Parse 16-bit timestamp.
                // Timestamp is transmitted as 2 bytes in big-endian order.
                byte[] timestampBytes = new byte[2];
                Array.Copy(packageBytes, _dataPayloadBytes, timestampBytes, 0, 2);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(timestampBytes);
                }
                ushort timeStamp = BitConverter.ToUInt16(timestampBytes, 0);

                UARTTimestampedData package = new UARTTimestampedData
                {
                    Data = dataValue,
                    Time = timeStamp
                };

                packages.Add(package);
            }
        }

        if (packages.Count > 0)
        {
            // Raise our custom event with the parsed packages.
            TimestampedDataReceived?.Invoke(this, new TimestampedDataReceivedEvent(packages.ToArray()));
        }
    }

    // Called upon application force shutdown to gracefully reset (and make ready) the
    // data transmission for a new connection
    private void ForceStopCleanup(object? sender, EventArgs e)
    {
        try
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;
            _serialPort.Write([(byte)'R'], 0, 1);
            _serialPort.BaseStream.Flush(); // Ensure the stop byte is sent
            System.Threading.Thread.Sleep(50); // Small delay to allow transmission
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error sending stop byte on force shutdown: " + ex.Message);
        }
        finally
        {
            try
            {
                _serialPort?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error gracefully closing port on force shutdown: " + ex.Message);
            }
        }
    }
}