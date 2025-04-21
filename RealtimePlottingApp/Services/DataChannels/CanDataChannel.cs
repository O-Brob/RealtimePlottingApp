using System.Collections.Generic;
using RealtimePlottingApp.Events;
using RealtimePlottingApp.Models;
using RealtimePlottingApp.Services.CAN;
using RealtimePlottingApp.Services.ConfigParsers;

namespace RealtimePlottingApp.Services.DataChannels;

public class CanDataChannel : IDataChannel
{
    // State variables for connections
    private readonly ICanBus _canBus;
    private readonly string _interfaceName;
    private readonly string? _bitrate;
    private readonly int _canIdFilter;
    private readonly string _canDataPayloadMask;
    private readonly IConfigParser _configParser = new ConfigParser();

    // Reference to model which data should be added to.
    private readonly GraphDataModel _graphDataModel;

    // ---------- Constructor ---------- //
    /// <summary>
    /// Creates a CanDataChannel, which will read data via CAN
    /// using the config paramters, and add the data into the
    /// provided GraphDataModel.
    /// </summary>
    /// <param name="canBus">The CAN-bus implementation to use</param>
    /// <param name="interfaceName">The CAN-interface to connect to</param>
    /// <param name="bitrate">The bit rate of the CAN communication</param>
    /// <param name="canIdFilter">An ID to filter data traffic by</param>
    /// <param name="canDataPayloadMask">A mask for which bytes of the data belongs to which variable</param>
    /// <param name="graphDataModel">The GraphDataModel to store received messages in after filtering</param>
    public CanDataChannel(ICanBus canBus, string interfaceName, string? bitrate, 
        int canIdFilter, string canDataPayloadMask, GraphDataModel graphDataModel)
    {
        _interfaceName = interfaceName;
        _bitrate = bitrate;
        _graphDataModel = graphDataModel;
        _canIdFilter = canIdFilter;
        _canDataPayloadMask = canDataPayloadMask;
        _canBus = canBus;
        _canBus.MessageReceived += OnCanDataReceived;
    }

    // ---------- IDataChannel Methods ---------- //
    public void Connect()
    {
        _canBus.Connect(_interfaceName, _bitrate);
    }

    public void Disconnect()
    {
        _canBus.Disconnect();
        _canBus.MessageReceived -= OnCanDataReceived;
    }
    
    // ---------- Implementation-specific helper methods ---------- //
    private void OnCanDataReceived(object? sender, CanMessageReceivedEvent e)
    {
        if (_canIdFilter != e.CanId) return; // Filter by requested ID
        
        List<uint> variables = _configParser.ParseCanDataMask(_canDataPayloadMask, e.Data);
        lock (_graphDataModel)
        {
            foreach (var value in variables)
            {
                _graphDataModel.AddPoint(e.Timestamp, value);
            }
        }
    }
}