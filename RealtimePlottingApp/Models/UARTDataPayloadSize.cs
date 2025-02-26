namespace RealtimePlottingApp.Models;

/// <summary>
/// Specifies the size of the data payload that is transmitted by a microcontroller
/// to be received by a serial reader, to allow an implementation to handle data accordingly.
/// </summary>
public enum UARTDataPayloadSize
{
    UART_PAYLOAD_8,  //  8-bit data ( 1 byte + 2 byte timestamp )
    UART_PAYLOAD_16, // 16-bit data ( 2 byte + 2 byte timestamp )
    UART_PAYLOAD_32, // 32-bit data ( 4 byte + 2 byte timestamp )
}