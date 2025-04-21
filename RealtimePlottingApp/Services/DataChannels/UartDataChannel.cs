using RealtimePlottingApp.Events;
using RealtimePlottingApp.Models;
using RealtimePlottingApp.Services.UART;

namespace RealtimePlottingApp.Services.DataChannels;

public class UartDataChannel : IDataChannel
{
    private readonly ISerialReader _serialReader;
    private readonly string _comPort;
    private readonly int _baudRate;
    private readonly UARTDataPayloadSize _dataSize;

    // Reference to model which data should be added to.
    private readonly GraphDataModel _graphDataModel;

    // ---------- Constructor ---------- //
    /// <summary>
    /// Creates a UartDataChannel, which will read data
    /// via UART using the provided parameters,
    /// and add the data to the given GraphDataModel.
    /// </summary>
    /// <param name="serialReader">The serial reader implementation to use</param>
    /// <param name="comPort">COM-Port for the serial stream</param>
    /// <param name="baudRate">The baud rate to receive data at</param>
    /// <param name="dataSize">The expected size of the data field in each package</param>
    /// <param name="graphDataModel">The data model to store receiving data in</param>
    public UartDataChannel(ISerialReader serialReader ,string comPort, int baudRate, UARTDataPayloadSize dataSize, GraphDataModel graphDataModel)
    {
        _comPort = comPort;
        _baudRate = baudRate;
        _dataSize = dataSize;
        _graphDataModel = graphDataModel;
        _serialReader = serialReader;
        _serialReader.TimestampedDataReceived += OnUartDataReceived;
    }

    // ---------- IDataChannel Methods ---------- //
    public void Connect()
    {
        _serialReader.StartSerial(_comPort, _baudRate, _dataSize);
    }

    public void Disconnect()
    {
        _serialReader.StopSerial();
        _serialReader.TimestampedDataReceived -= OnUartDataReceived;
    }
    
    // ---------- Implementation-specific helper methods ---------- //
    private void OnUartDataReceived(object? sender, TimestampedDataReceivedEvent e)
    {
        lock (_graphDataModel)
        {
            foreach (var package in e.Packages)
            {
                // Add timestamp and accompanied data.
                _graphDataModel.AddPoint(package.Time, package.Data);
            }
        }
    }
}