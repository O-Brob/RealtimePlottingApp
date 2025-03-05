using System;
using System.Collections.Generic;

namespace RealtimePlottingApp.Models;

/// <summary>
/// Model representing data lists for a 2D plotting of X and Y values.
/// </summary>
public class GraphDataModel
{
    // Private instance variables which stores the data
    private readonly List<uint> _XData;
    private readonly List<uint> _YData;
    
    // Variables for timestamp (X-Value) overflow handling.
    // _lastRawTimestamp keeps track of the last raw timestamp.
    // _overflowAdd holds the total offset added to the raw timestamp.
    private uint _lastRawTimestamp;
    private uint _overflowAdd;
    private uint _xValBitSize = 16; // 16-bit timestamps as default.

    /// <summary>
    /// Variable defining bit-size of the X-Axis values,
    /// to account for overflows from the received data when plotting.
    /// </summary>
    public uint XValBitSize
    {
        get => _xValBitSize;
        set => _xValBitSize = value;
    }
    
    /// <summary>
    /// Counter that increments each time a timestamp overflow is detected.
    /// Overflows are handled internally to allow consistent plotting.
    /// </summary>
    public uint yOverflowCounter { get; private set; }
    
    /// <summary>
    /// Returns access to a read-only representation of the X values.
    /// </summary>
    public IReadOnlyList<uint> XData => _XData.AsReadOnly();
    
    /// <summary>
    /// Returns access to a read-only representation of the Y values.
    /// </summary>
    public IReadOnlyList<uint> YData => _YData.AsReadOnly();

    /// <summary>
    /// Constructor for a new graph data model
    /// </summary>
    public GraphDataModel()
    {
        _XData = [];
        _YData = [];
        _lastRawTimestamp = 0;
        _overflowAdd = 0;
        yOverflowCounter = 0;
    }

    /// <summary>
    /// Clone constructor for a graph data model 
    /// </summary>
    /// <param name="xData">A list of data values for the x-axis</param>
    /// <param name="yData">A list of data values for the y-axis</param>
    public GraphDataModel(List<uint> xData, List<uint> yData)
    {
        _XData = new List<uint>(xData);
        _YData = new List<uint>(yData);
        _lastRawTimestamp = 0;
        _overflowAdd = 0;
        yOverflowCounter = 0;
    }

    /// <summary>
    /// Adds a new data point to both the X and Y collections of data.
    /// The X values are adjusted dynamically to account for potential timestamp overflows.
    /// </summary>
    /// <param name="x">The raw timestamp value.</param>
    /// <param name="y">The Y value data.</param>
    public void AddPoint(uint x, uint y)
    {
        // Check for overflow for values that is not the first value.
        if (_XData.Count > 0)
        {
            // When a new "raw timestamp" is less than the previous one, an overflow must have occurred.
            if (x < _lastRawTimestamp)
            {
                // Increment the value to "make up for" by overflows to ensure graphing integrity
                _overflowAdd += (uint)Math.Pow(2,_xValBitSize); //The amount "lost" during this X-Axis overflow.
                yOverflowCounter++;
            }
        }
            
        // Store the current raw timestamp, to allow next value to check if it's overflown.
        _lastRawTimestamp = x;

        // Calculate the adjusted timestamp by adding the accumulated offset from any and all overflows.
        uint adjustedX = x + _overflowAdd;
        _XData.Add(adjustedX);
        _YData.Add(y);
    }

    public void Clear()
    {
        _XData.Clear();
        _YData.Clear();
        _lastRawTimestamp = 0;
        _overflowAdd = 0;
        yOverflowCounter = 0;
    }
}