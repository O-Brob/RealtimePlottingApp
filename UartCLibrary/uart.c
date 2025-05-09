/**********************************************************************************************************************
* Includes
**********************************************************************************************************************/

#include "uart.h"

/**********************************************************************************************************************
* Private Defines
**********************************************************************************************************************/
/**
  * @brief A payload containing up to 32 bits of data and a 8-bit timestamp.
  *
  * The data field always occupies 32 bits in memory, but only the lower 8, 16, or 32 bits are used,
  * based on the selected payload size. The timestamp is always 8 bits.
  */
typedef struct {
    uint32_t data; // MAX size of data
    uint8_t timestamp;
}UART_TimestampedData;

/**********************************************************************************************************************
* Private Variables
**********************************************************************************************************************/

// Internal transmit buffer
static UART_TimestampedData uartTxBuffer[UART_TX_BUFFER_SIZE];  // Transmit buffer
static uint16_t uartTxHead;  // Head of the transmit buffer (Data write position)
static uint16_t uartTxTail;  // Tail of the transmit buffer (Data read position)

// Pointer to the currently used UART instance, so we can access it and its flags after initialization.
static USART_TypeDef *selectedUART = NULL;

// Pointer to the timer to be used for timestamping the data. Resolution of timer increments is left to the user.
// Marked volatile to ensure data access via pointer is treated as volatile, due to probable interrupt increments.
static volatile uint32_t *timeValue = NULL;

// Variable to store the payload size selected by the user during initialization.
static UART_PayloadSize uartPayloadSize;

// Flag to control transmission (off by default)
static uint8_t uartTransmitEnabled = 0;

/**********************************************************************************************************************
* Private Function Prototypes
**********************************************************************************************************************/

static int UART_ProcessCommand(void);

/**********************************************************************************************************************
* Private Macros
**********************************************************************************************************************/
//----- Circular buffer macros -----//
// "Returns" the next index, taking buffer wrap-around into account.
#define BUFFER_NEXT(index) (((index) + 1) % UART_TX_BUFFER_SIZE)
// "Returns" whether head is caught up to tail, representing a full buffer.
#define BUFFER_IS_FULL() (BUFFER_NEXT(uartTxHead) == uartTxTail)
// "Returns" whether head and tail is on same position, representing empty buffer.
#define BUFFER_IS_EMPTY() (uartTxHead == uartTxTail)

/**********************************************************************************************************************
* API Function Definitions
**********************************************************************************************************************/

/**
 * @brief  Initializes the specified USART for asynchronous communication.
 * @param  USARTx: A pointer to the USART instance (e.g., USART3).
 * @param  timestampHolder: A pointer to a variable that is periodically incremented
 *         by a timer interrupt, at the desired resolution.
 * @param  payloadSize: The size of the data payload to be timestamped and transmitted.
 *         Valid options are UART_PAYLOAD_8 (uint8_t), UART_PAYLOAD_16 (uint16_t), or UART_PAYLOAD_32 (uint32_t).
 */
void UART_Init(USART_TypeDef *USARTx, uint32_t *timestampHolder, UART_PayloadSize payloadSize) {
    // Store initialized values in state.
    selectedUART = USARTx;
    timeValue = timestampHolder;
    uartPayloadSize = payloadSize;
    uartTxHead = 0; uartTxTail = 0;

    // Enforce data-width = 8, and disabled parity
    LL_USART_SetDataWidth(USARTx, LL_USART_DATAWIDTH_8B);
    LL_USART_SetParity(USARTx, LL_USART_PARITY_NONE);

    // Additional configuration (baud rate, etc.) should have been done already via CubeMX
    // for the given USART_TypeDef, or can be manually added here if needed.

    //LL_USART_SetBaudRate(USARTx, HAL_RCC_GetPCLK1Freq(), LL_USART_GetOverSampling(USARTx), 921600);

    LL_USART_Enable(USARTx);
}

/**
 * @brief  Buffers one or more data values to be transmitted over the selected USART.
 *
 * Only the lower 8, 16, or 32 bits of each data variable is used depending on the selected
 * payload size during initialization. The lowest 8 bits of the timestamp at the time
 * will be accompanied with each value of data that is stored. The order of the pointers
 * in the array defines the order in which the timestamped data packets are added and
 * transmitted from the internal circular buffer.
 *
 * @param  dataArray: Array of pointers to data values to be timestamped and stored.
 *         The lower bits are used according to the selected payload size.
 *         The referenced values are expected to adhere to the selected payload size.
 * @param  n: The number of data pointers in the array.
 *
 * @return 1 if all data values were successfully added to the buffer, or 0
 *         if there isn’t enough room, initialization has not been performed, or transmission is disabled.
 */
 // It must be the case that only the data on uartTxHead is changed to maintain circular buffer characteristics:
 // * forall i :: (0 <= i < uartTxBuffer.Length && i != old(uartTxHead)) ==> uartTxBuffer[i] == old(uartTxBuffer[i]))
int UART_StoreData(uint32_t *dataArray[], size_t n) {
    if(selectedUART == NULL){
        // Initialization not done, don't store data,
        // return 0 to signal.
        return 0;
    }

    // Process any incoming command before taking action
    UART_ProcessCommand();
    if(!uartTransmitEnabled){
        // Stop storing data if stop signal command been given
        return 0;
    }

    // Calculate number of available slots in the circular buffer
    size_t freeSlots;
    if(uartTxHead >= uartTxTail){
        freeSlots = UART_TX_BUFFER_SIZE - (uartTxHead - uartTxTail) - 1;
    } else {
        freeSlots = uartTxTail - uartTxHead - 1;
    }

    // Check if there's enough room to construct one packet per data variable.
    if(freeSlots < n){
        return 0; // Not enough room
    }

    // Loop through each pointer in dataArray to create packets.
    for(size_t i = 0; i < n; i++) {
        // Have dataToAdd point to the next free buffer slot
        UART_TimestampedData *dataToAdd = &uartTxBuffer[uartTxHead];
        uint32_t dataValue = *dataArray[i];

        // Mask the data according to selected payload size
        switch (uartPayloadSize) {
            case UART_PAYLOAD_8:
                dataToAdd->data = dataValue & 0xFF;
                break;
            case UART_PAYLOAD_16:
                dataToAdd->data = dataValue & 0xFFFF;
                break;
            default: // UART_PAYLOAD_32, don't mask.
                dataToAdd->data = dataValue;
                break;
        }

        // Use 8 bits of a provided "timestamp variable".
        dataToAdd->timestamp = (uint8_t)(*timeValue & 0xFF);

        // Data added, move head forward:
        uartTxHead = BUFFER_NEXT(uartTxHead);
    }

    return 1; // Data added, return success.
}

/**
 * @brief Flushes a single UART_TimestampedData packet from the TX buffer.
 *
 * This function transmits one data packet from the buffer.
 * The packet format is:
 *    (8, 16, or 32 bits of data) followed by (8 bits of timestamp).
 *
 * @return 1 if the transmission was successful, or 0 if there is nothing in the buffer, or transmission is disabled.
 */
int UART_FlushOne(void) {
    // Check if there's any data to send
    if (BUFFER_IS_EMPTY() || selectedUART == NULL) {
        // Buffer is empty or initialization not done
        return 0;
    }

    // Process any incoming command before taking action
    UART_ProcessCommand();
    if(!uartTransmitEnabled){
        // Stop flushing data if stop signal command been given
        return 0;
    }

    // Get the next packet in line to send
    UART_TimestampedData packet = uartTxBuffer[uartTxTail];
    uint8_t bytesToTransmit[uartPayloadSize + 1]; // Max 4 bytes data + 1 bytes timestamp.
    uint8_t byteCount;

    // Pack data to send based on payload size
    switch(uartPayloadSize){
        case UART_PAYLOAD_8:
            bytesToTransmit[0] = packet.data & 0xFF;
            byteCount = 1;
            break;
        case UART_PAYLOAD_16:
            bytesToTransmit[0] = (packet.data >> 8) & 0xFF;
            bytesToTransmit[1] = packet.data & 0xFF;
            byteCount = 2;
            break;
        default: // UART_PAYLOAD_32
            bytesToTransmit[0] = (packet.data >> 24) & 0xFF;
            bytesToTransmit[1] = (packet.data >> 16) & 0xFF;
            bytesToTransmit[2] = (packet.data >> 8) & 0xFF;
            bytesToTransmit[3] = packet.data & 0xFF;
            byteCount = 4;
            break;
    }

    // Add the least significant byte of the timestamp
    bytesToTransmit[byteCount++] = packet.timestamp & 0xFF;

    // Transmit all bytes for this packet
    for(int i = 0; i < byteCount; i++){
        int timeout = 5000;
        while(!LL_USART_IsActiveFlag_TXE(selectedUART)) { if (timeout-- <= 0) return 0; }
        LL_USART_TransmitData8(selectedUART, bytesToTransmit[i]);
    }

    // Wait until transmission is fully done.
    while (!LL_USART_IsActiveFlag_TC(selectedUART)) { /* Wait until transmission is done */ }

    // Move the tail forward for the next package
    uartTxTail = BUFFER_NEXT(uartTxTail);

    return 1; // Data sent, return success.
}

/**
 * @brief  Flushes the internal TX buffer by sending all its data over the USART.
 *
 * This function transmits every buffered data packet.
 * Each packet's format is:
 *    (8, 16, or 32 bits of data) followed by (8 bits of timestamp).
 *
 * @return 1 if transmission was successful, or 0 if there is nothing to send, or transmission is disabled.
 *
 * @note This implementation does not call UART_FlushOne() to avoid the overhead of repeated function calls,
 *       but works the same way otherwise, except that it flushes the entire buffer.
 */
int UART_FlushBuffer(void) {
    // Check if there's any data to send
    if (BUFFER_IS_EMPTY() || selectedUART == NULL) {
        // Buffer is empty or initialization not done
        return 0;
    }

    // Send data until buffer is empty
    while(!BUFFER_IS_EMPTY()){
        // Process any incoming command before taking action
        UART_ProcessCommand();
        if(!uartTransmitEnabled){
            // Stop flush loop if stop signal command been given
            return 0;
        }

        // Get the next packet in line to send
        UART_TimestampedData packet = uartTxBuffer[uartTxTail];
        uint8_t bytesToTransmit[uartPayloadSize + 1]; // Max 4 bytes data + 1 bytes timestamp
        uint8_t byteCount;

        // Pack data to send based on payload size
        switch (uartPayloadSize) {
            case UART_PAYLOAD_8:
                bytesToTransmit[0] = packet.data & 0xFF;
                byteCount = 1;
                break;
            case UART_PAYLOAD_16:
                bytesToTransmit[0] = (packet.data >> 8) & 0xFF;
                bytesToTransmit[1] = packet.data & 0xFF;
                byteCount = 2;
                break;
            default: // UART_PAYLOAD_32
                bytesToTransmit[0] = (packet.data >> 24) & 0xFF;
                bytesToTransmit[1] = (packet.data >> 16) & 0xFF;
                bytesToTransmit[2] = (packet.data >> 8) & 0xFF;
                bytesToTransmit[3] = packet.data & 0xFF;
                byteCount = 4;
                break;
        }

        // Add the least significant byte of the timestamp
        bytesToTransmit[byteCount++] = packet.timestamp & 0xFF;

        // Transmit all bytes for this packet
        for (int i = 0; i < byteCount; i++) {
            int timeout = 5000;
            while(!LL_USART_IsActiveFlag_TXE(selectedUART)) { if (timeout-- <= 0) return 0; }
            LL_USART_TransmitData8(selectedUART, bytesToTransmit[i]);
        }

        // Move the tail forward, so we can send the next package until the buffer is empty
        uartTxTail = BUFFER_NEXT(uartTxTail);
    }

    // Wait until transmission is fully done.
    while (!LL_USART_IsActiveFlag_TC(selectedUART)) { /* Wait until transmission is done */ }

    return 1; // Data sent, return success.
}

/**********************************************************************************************************************
* Private Function Definitions
**********************************************************************************************************************/

/**
 * @brief Checks for received commands and processes them.
 *
 * Commands:\n
 *   'S' - Start transmission (set uartTransmitEnabled to 1)\n
 *   'R' - Reset transmission (set uartTransmitEnabled to 0 to pause transmission, and reset head/tail tx-pointers)\n
 *
 * This function should be called before transmitting/flushing data.
 *
 * @returns 1 if a command was processed, 0 if there was no command to process.
 */
static int UART_ProcessCommand(void){
    // If `RX Not Empty` flag is set --> command byte is waiting in register
    if(LL_USART_IsActiveFlag_RXNE(selectedUART)){

        // Receive the command byte and handle/reject it
        uint8_t commandByte = LL_USART_ReceiveData8(selectedUART);

        switch(commandByte){
            case 'S': // Start transmission!
                uartTransmitEnabled = 1;
                break;
            case 'R': // Reset transmission (stop + reset buffer pointers)
                uartTransmitEnabled = 0;
                uartTxHead = 0;
                uartTxTail = 0;
                break;
            default:
                // Do nothing, invalid command received.
                break;
        }

        // Command was received and processed.
        return 1;
    }
    // No command was received
    return 0;
}
