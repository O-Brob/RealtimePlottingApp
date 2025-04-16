using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealtimePlottingApp.Models;

namespace RealtimePlottingApp.Services.ConfigParsers;

public partial class ConfigParser : IConfigParser
{
    public bool ParseUartConfig(string message, out string comPort, out int baudRate, out UARTDataPayloadSize dataSize, out int uniqueVars)
    {
        const string pattern = @"^ConnectUart:ComPort:(?<comPort>[^,]+),BaudRate:(?<baudRate>\d+),DataSize:(?<dataSize>\d+ bits),UniqueVars:(?<uniqueVars>\d+)$";
        Match match = GeneratedUartRegex().Match(message);

        if (match.Success)
        {
            comPort = match.Groups["comPort"].Value;
            baudRate = int.Parse(match.Groups["baudRate"].Value);
            uniqueVars = int.Parse(match.Groups["uniqueVars"].Value);
            dataSize = match.Groups["dataSize"].Value switch
            {
                "8 bits" => UARTDataPayloadSize.UART_PAYLOAD_8,
                "16 bits" => UARTDataPayloadSize.UART_PAYLOAD_16,
                "32 bits" => UARTDataPayloadSize.UART_PAYLOAD_32,
                _ => throw new FormatException("Invalid data size format")
            };
            return true;
        }

        // No success, return defaults.
        comPort = string.Empty;
        baudRate = 0;
        uniqueVars = 0;
        dataSize = default;
        return false;
    }
    
    public bool ParseCanConfig(string message, out string canInterface, out string bitRate, out int canIdFilter, out string dataPayloadMask)
    {
        const string pattern = @"^ConnectCan:CanInterface:(?<canInterface>[^,]+),BitRate:(?<bitRate>[^,]+),CanIdFilter:(?<canIdFilter>\d+),DataPayloadMask:(?<dataPayloadMask>.+)$";
        Match match = GeneratedCanRegex().Match(message);

        if (match.Success)
        {
            canInterface = match.Groups["canInterface"].Value;
            bitRate = match.Groups["bitRate"].Value;
            canIdFilter = int.Parse(match.Groups["canIdFilter"].Value);
            dataPayloadMask = match.Groups["dataPayloadMask"].Value;
            return true;
        }

        // No success, return defaults.
        canInterface = string.Empty;
        bitRate = string.Empty;
        canIdFilter = 0;
        dataPayloadMask = string.Empty;
        return false;
    }
    
    /// <summary>
    /// Parses a CAN data payload mask to extract the variables from the 8-byte data array.
    /// The mask is expected to be in the format "__:__:__:__:__:__:__:__", with each colon-seperated
    /// group corresponds to the first and second half of a byte (imagine it as a hex-representation of the payload).
    /// </summary>
    /// <param name="mask">The mask string</param>
    /// <param name="data">A byte-array of size 8 containing CAN data</param>
    /// <returns>List representing the extracted variables as unsigned integers indexed by numerical order.</returns>
    public List<uint> ParseCanDataMask(string mask, byte[] data)
    {
        List<uint> variables = [];
        
        string[] groups = mask.Split(':');
        if (groups.Length != 8)
        {
            Console.WriteLine("Invalid mask format. Expected 8 groups.");
            // Return empty list
            return variables;
        }

        // For each possible variable number 1..9 which exists in the mask:
        for (int i = 1; mask.Contains(i.ToString()) && i <= 9; i++)
        {
            uint varI = 0;
            bool variableUpdated = false;
            // Look for number i in the mask, to construct variable i.
            for (int j = 0; j < groups.Length; j++)
            {
                // Variable not masked in this group(byte), check next.
                if (!groups[j].Contains(i.ToString())) continue;
                
                if (groups[j].Equals($"{i}{i}")) // Case: "ii"
                {
                    // Shift left by 8 bits (full byte) and `|` with data to reconstruct that full byte of the data.
                    varI = (varI << 8) | data[j];
                    variableUpdated = true;
                }
                
                else if (groups[j].StartsWith($"{i}")) // Case: "i_", where _ is wildcard.
                {
                    uint highHalf = (uint)(data[j] >> 4) & 0x0F; // Extract high half
                    varI = (varI << 4) | highHalf; // Shift left by 4 bits and `|` with the half
                    variableUpdated = true;
                }
                
                else if (groups[j].EndsWith($"{i}")) // Case "_i", where _ is wildcard.
                {
                    uint lowHalf = (uint)data[j] & 0x0F; // Extract low half
                    varI = (varI << 4) | lowHalf; // Shift left by 4 bits and `|` with the half
                    variableUpdated = true;
                }
            }
            // Finished constructing data from mask.
            if(variableUpdated)
                variables.Add(varI);
        }

        return variables;
    }

    // Generated Regexes for higher performance than defining on the spot.
    [GeneratedRegex(@"^ConnectUart:ComPort:(?<comPort>[^,]+),BaudRate:(?<baudRate>\d+),DataSize:(?<dataSize>\d+ bits),UniqueVars:(?<uniqueVars>\d+)$")]
    private static partial Regex GeneratedUartRegex();
    [GeneratedRegex(@"^ConnectCan:CanInterface:(?<canInterface>[^,]+),BitRate:(?<bitRate>[^,]+),CanIdFilter:(?<canIdFilter>\d+),DataPayloadMask:(?<dataPayloadMask>.+)$")]
    private static partial Regex GeneratedCanRegex();
}