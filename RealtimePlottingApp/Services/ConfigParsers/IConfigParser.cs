using System.Collections.Generic;
using RealtimePlottingApp.Models;

namespace RealtimePlottingApp.Services.ConfigParsers;

public interface IConfigParser
{
    /// <summary>
    /// Parses a UART config string and returns mandatory
    /// fields to facilitate a connection to UART.
    /// </summary>
    /// <param name="message">String containing user configuration</param>
    /// <param name="comPort">The com-port to connect to</param>
    /// <param name="baudRate">The baud rate to communicate at</param>
    /// <param name="dataSize">The size of the data that is received</param>
    /// <param name="uniqueVars">The number of unique variables expected to be received</param>
    /// <returns></returns>
     bool ParseUartConfig(string message, out string comPort, out int baudRate,
        out UARTDataPayloadSize dataSize, out int uniqueVars);

    /// <summary>
    /// Parses a CAN config string and returns mandatory
    /// fields to facilitate a connection to CAN
    /// </summary>
    /// <param name="message">String containing user configuration</param>
    /// <param name="canInterface">The CAN-Interface to connect to</param>
    /// <param name="bitRate">The bit rate to communicate at</param>
    /// <param name="canIdFilter">The ID of the can messages to listen to</param>
    /// <param name="dataPayloadMask">A mask defining how to read the data</param>
    /// <returns></returns>
    bool ParseCanConfig(string message, out string canInterface, out string bitRate,
        out int canIdFilter, out string dataPayloadMask);

    /// <summary>
    /// Parses a CAN data mask to define what data in the payload
    /// belongs to which variable.
    /// </summary>
    /// <param name="mask">The data mask to be parsed</param>
    /// <param name="data">The byte array of data to apply the mask to</param>
    /// <returns></returns>
    List<uint> ParseCanDataMask(string mask, byte[] data);
}