namespace RealtimePlottingApp.Models;

/// <summary>
/// Represents one package received from a serial reader, consisting of data value and timestamp.
/// </summary>
public struct UARTTimestampedData
{
    /// <summary>
    /// Data value stored as 32 bits, even if transmitted data is smaller.
    /// </summary>
    public uint Data { get; set; }
    
    /// <summary>
    /// A timestamp which accompanies the data.
    /// </summary>
    public ushort Time { get; set; }
}