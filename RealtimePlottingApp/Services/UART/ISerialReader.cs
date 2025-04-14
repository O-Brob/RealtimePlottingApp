using System;
using RealtimePlottingApp.Events;
using RealtimePlottingApp.Models;

namespace RealtimePlottingApp.Services.UART;

/// <summary>
/// Interface for a serial port reader that receives timestamped data packages on given com-port.
/// </summary>
public interface ISerialReader
{
    /// <summary>
    /// Starts serial communication on given comPort with the provided baud rate.
    /// Sends a start byte 'S' to the serial connection, opens a port, and begins asynchronous reading.
    /// </summary>
    /// <param name="comPort">The COM port (ex: "COM1" or "/dev/ttyS0")</param>
    /// <param name="baudRate">The baud rate for the serial connection</param>
    /// <param name="payloadDataSize">Specifies whether the data size is 8, 16, or 32 bits</param>
    void StartSerial(string comPort, int baudRate, UARTDataPayloadSize payloadDataSize);

    /// <summary>
    /// Stops the serial communication for the object.
    /// Sends an 'R' byte to the serial connection and stops the asynchronous reading loop.
    /// </summary>
    void StopSerial();
    
    /// <summary>
    /// Event to be raised when one or more complete timestamped data packages have been received.
    /// </summary>
    event EventHandler<TimestampedDataReceivedEvent> TimestampedDataReceived; 
}