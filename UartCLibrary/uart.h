/**********************************************************************************************************************
 * (Made as a part of a Chalmers graduation project)
 *
 * This header file defines the interface for the UART library to send timestamped data.
 * It is made to be compatible with the following application, which can visualize
 * the transmitted data through plotting: https://github.com/O-Brob/RealtimePlottingApp
 *
 * The following allows for the initialization, data transmission, and timestamping
 * of data packets over the peripheral on an STM32F4 microcontroller.
 *
 * The following functionality is provided:
 * - Initialization of USART peripherals for asynchronous communication.
 * - Storage of timestamped data packets to be transmitted over UART.
 * - Flushing of stored data either one packet at a time or all at once.
 *
 * Supports flexible payload sizes to be set during initialization (8, 16, or 32 bits), and
 * can operate with a timestamp that is periodically incremented by a timer interrupt.
 *
 * The UART communication is set up to transmit data in a circular buffer.
 * It only starts transmitting over TX after receiving an 'S' byte in the Read Data Register,
 * and stops + resets the internal buffer on receiving an 'R' byte.
 *
 * Notes:
 * - No alignment byte is sent between packages, and as such, data should not be stored
 *   in different orders (as indicated by the pointer array) on sequential stores,
 *   and preferably not be done in multiple separate interrupt handlers at once.
 *
 * - When a payload size is set (16 bits, for example), every variable stored is expected to
 *   be at the set size or lower.
 *
 * - The timestamp is assumed to be provided by an external timer interrupt
 *   and is used to timestamp data packets before transmission.
 **********************************************************************************************************************/

/* Define to prevent recursive inclusion ----------------------------------------------------------------------------*/
#ifndef __UART_H
#define __UART_H

/**********************************************************************************************************************
* Includes
**********************************************************************************************************************/

#include <stdint.h>
#include <stddef.h>
#include "stm32f4xx_ll_usart.h"

/**********************************************************************************************************************
* Typedefs
**********************************************************************************************************************/
typedef enum { // Defines valid lengths of the data to timestamp and transmit
    UART_PAYLOAD_8  = 1, // 1 byte(s)
    UART_PAYLOAD_16 = 2, // 2 byte(s)
    UART_PAYLOAD_32 = 4, // 4 byte(s)
} UART_PayloadSize;

/**********************************************************************************************************************
* Defines
**********************************************************************************************************************/

/* Define the size of the internal transmit buffer */
#define UART_TX_BUFFER_SIZE 128
//#define UART_RX_BUFFER_SIZE 128 // If we ever want to read more complex commands (>1byte size). Currently not needed.

/**********************************************************************************************************************
* API Function Declarations
**********************************************************************************************************************/

/**
 * @brief  Initializes the specified USART for asynchronous communication.
 * @param  USARTx: A pointer to the USART instance (e.g., USART3).
 * @param  timestampHolder: A pointer to a variable that is periodically incremented
 *         by a timer interrupt, at the desired resolution.
 * @param  payloadSize: The size of the data payload to be timestamped and transmitted.
 *         Valid options are UART_PAYLOAD_8 (uint8_t), UART_PAYLOAD_16 (uint16_t), or UART_PAYLOAD_32 (uint32_t).
 */
void UART_Init(USART_TypeDef *USARTx, uint32_t *timestampHolder, UART_PayloadSize payloadSize);

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
int UART_StoreData(uint32_t *dataArray[], size_t n);

/**
 * @brief Flushes a single UART_TimestampedData packet from the TX buffer.
 *
 * This function transmits one data packet from the buffer.
 * The packet format is:
 *    (8, 16, or 32 bits of data) followed by (8 bits of timestamp).
 *
 * @return 1 if the transmission was successful, or 0 if there is nothing in the buffer, or transmission is disabled.
 */
int UART_FlushOne(void);

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
int UART_FlushBuffer(void);

#endif //__UART_H