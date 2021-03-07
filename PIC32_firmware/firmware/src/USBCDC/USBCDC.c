/* ************************************************************************** */
/** Descriptive File Name

  @Company
    Company Name

  @File Name
    filename.c

  @Summary
    Brief description of the file.

  @Description
    Describe the purpose of this file.
 */
/* ************************************************************************** */

/* ************************************************************************** */
/* ************************************************************************** */
/* Section: Included Files                                                    */
/* ************************************************************************** */
/* ************************************************************************** */

#include "USBCDC.h"
#include "app.h"

/* ************************************************************************** */
/* ************************************************************************** */
/* Section: File Scope or Global Data                                         */
/* ************************************************************************** */
/* ************************************************************************** */

QueueHandle_t qh_USBDeviceTask_EventQueue_Handle;
extern QueueHandle_t qh_SERVOPOSITIONS_Queue;

// Read Data Buffer
uint8_t USBCDC_ReadBuffer[USBCDC_BUFFER_SIZE] ;

// Write Data Buffer
char USBCDC_WriteBuffer[USBCDC_BUFFER_SIZE];

USBCDC_DATA usbcdcData;
TaskHandle_t th_USBDevice_Task = NULL;

/* ************************************************************************** */
/* ************************************************************************** */
// Section: Local Functions                                                   */
/* ************************************************************************** */
/* ************************************************************************** */

// *****************************************************************************
// *****************************************************************************
// Section: Application Callback Routines
// *****************************************************************************
// *****************************************************************************
// These routines are called by drivers when certain events occur.

void APP_USBDeviceEventHandler(USB_DEVICE_EVENT event, void * pData, uintptr_t context);

USB_DEVICE_CDC_EVENT_RESPONSE APP_USBDeviceCDCEventHandler (USB_DEVICE_CDC_INDEX index , USB_DEVICE_CDC_EVENT event ,void* pData, uintptr_t userData);

/*************************************************
 * Application Device Layer Event Handler
 *************************************************/

void APP_USBDeviceEventHandler(USB_DEVICE_EVENT event, void * pData, uintptr_t context)
{
    uint8_t configurationValue;
    USBCDC_intern USB_Event;
    USB_Event.data = 0;
    USB_Event.size = 0;
    portBASE_TYPE xHigherPriorityTaskWoken = pdFALSE;

    switch( event )
    {
        case USB_DEVICE_EVENT_POWER_REMOVED:
            /* Attach the device */
            USB_DEVICE_Detach (usbcdcData.deviceHandle);
            break;
        case USB_DEVICE_EVENT_RESET:
        case USB_DEVICE_EVENT_DECONFIGURED:

            /* Device was either de-configured or reset */
            break;

        case USB_DEVICE_EVENT_CONFIGURED:

            /* pData will point to the configuration. Check the configuration */
            configurationValue = ((USB_DEVICE_EVENT_DATA_CONFIGURED *)pData)->configurationValue;
            if(configurationValue == 1)
            {
                // Register the CDC Device application event handler here.
                // Note how the usbcdcData object pointer is passed as the user data.
                USB_DEVICE_CDC_EventHandlerSet(USB_DEVICE_CDC_INDEX_0, APP_USBDeviceCDCEventHandler, (uintptr_t)&usbcdcData);

                // let processing USB Task know USB if configured
                USB_Event.data = USBDEVICETASK_USBCONFIGURED_EVENT;
                USB_Event.size = 0;
                
                xQueueSendToBackFromISR(qh_USBDeviceTask_EventQueue_Handle, &USB_Event, &xHigherPriorityTaskWoken);
                portEND_SWITCHING_ISR( xHigherPriorityTaskWoken );
            }

            break;

        case USB_DEVICE_EVENT_SUSPENDED:
            break;

        case USB_DEVICE_EVENT_RESUMED:
            break;
        case USB_DEVICE_EVENT_POWER_DETECTED:
            // let processing USB Task know USB is powered
            USB_Event.data = USBDEVICETASK_USBPOWERED_EVENT;
            USB_Event.size = 0;

            xQueueSendToBackFromISR(qh_USBDeviceTask_EventQueue_Handle, &USB_Event, &xHigherPriorityTaskWoken);
            portEND_SWITCHING_ISR( xHigherPriorityTaskWoken );
            /* Attach the device */
            //USB_DEVICE_Attach (appData.deviceHandle);
            break;
        case USB_DEVICE_EVENT_ERROR:
        default:
            break;
    }
}


/************************************************
 * CDC Function Driver Application Event Handler
 ************************************************/

USB_DEVICE_CDC_EVENT_RESPONSE APP_USBDeviceCDCEventHandler
(
    USB_DEVICE_CDC_INDEX index ,
    USB_DEVICE_CDC_EVENT event ,
    void* pData,
    uintptr_t userData
)
{
    USBCDC_DATA * usbcdcDataObject;
    usbcdcDataObject = (USBCDC_DATA *)userData;
    USB_CDC_CONTROL_LINE_STATE * controlLineStateData;
    uint16_t * breakData;
    USBCDC_intern USB_Event;
    USB_Event.data = 0;
    USB_Event.size = 0;
    portBASE_TYPE xHigherPriorityTaskWoken = pdFALSE;

    switch ( event )
    {
        case USB_DEVICE_CDC_EVENT_GET_LINE_CODING:
            /* This means the host wants to know the current line
             * coding. This is a control transfer request. Use the
             * USB_DEVICE_ControlSend() function to send the data to
             * host.  */
            USB_DEVICE_ControlSend(usbcdcDataObject->deviceHandle, &usbcdcDataObject->usbcdcPort.getLineCodingData, sizeof(USB_CDC_LINE_CODING));
            break;

        case USB_DEVICE_CDC_EVENT_SET_LINE_CODING:
            /* This means the host wants to set the line coding.
             * This is a control transfer request. Use the
             * USB_DEVICE_ControlReceive() function to receive the
             * data from the host */
            USB_DEVICE_ControlReceive(usbcdcDataObject->deviceHandle, &usbcdcDataObject->usbcdcPort.setLineCodingData, sizeof(USB_CDC_LINE_CODING));
            break;

        case USB_DEVICE_CDC_EVENT_SET_CONTROL_LINE_STATE:
            /* This means the host is setting the control line state.
             * Read the control line state. We will accept this request
             * for now. */
            controlLineStateData = (USB_CDC_CONTROL_LINE_STATE *)pData;
            usbcdcDataObject->usbcdcPort.controlLineStateData.dtr = controlLineStateData->dtr;
            usbcdcDataObject->usbcdcPort.controlLineStateData.carrier = controlLineStateData->carrier;
            USB_DEVICE_ControlStatus(usbcdcDataObject->deviceHandle, USB_DEVICE_CONTROL_STATUS_OK);
            break;

        case USB_DEVICE_CDC_EVENT_SEND_BREAK:
            /* This means that the host is requesting that a break of the
             * specified duration be sent. Read the break duration */
            breakData = (uint16_t *)pData;
            usbcdcDataObject->usbcdcPort.breakData = *breakData;
            break;

        case USB_DEVICE_CDC_EVENT_READ_COMPLETE:
            USB_Event.data = USBDEVICETASK_READDONECOM_EVENT;
            USB_Event.size=((USB_DEVICE_CDC_EVENT_DATA_READ_COMPLETE *)pData)->length;
            // let processing USB Task know USB if configured
            xQueueSendToBackFromISR(qh_USBDeviceTask_EventQueue_Handle, &USB_Event, &xHigherPriorityTaskWoken);
            portEND_SWITCHING_ISR( xHigherPriorityTaskWoken );
            break;

        case USB_DEVICE_CDC_EVENT_CONTROL_TRANSFER_DATA_RECEIVED:
            // The data stage of the last control transfer is complete. For now we accept all the data.
            USB_DEVICE_ControlStatus(usbcdcDataObject->deviceHandle, USB_DEVICE_CONTROL_STATUS_OK);
            break;

        case USB_DEVICE_CDC_EVENT_CONTROL_TRANSFER_DATA_SENT:
            // This means the GET LINE CODING function data is valid. We don't do much with this data here.
            break;

        case USB_DEVICE_CDC_EVENT_WRITE_COMPLETE:
            USB_Event.data = USBDEVICETASK_WRITEDONECOM_EVENT;
            USB_Event.size = 0;
            // let processing USB Task know USB if configured
            
            xQueueSendToBackFromISR(qh_USBDeviceTask_EventQueue_Handle, &USB_Event, &xHigherPriorityTaskWoken);
            portEND_SWITCHING_ISR( xHigherPriorityTaskWoken );
            break;

        default:
            break;
    }
    return USB_DEVICE_CDC_EVENT_RESPONSE_NONE;
}


// *****************************************************************************
/* Function:
    void USBDevice_Task(void)

  Summary:
    It is an RTOS task for Attaching and Configuring USB Device to Host.

  Description:
    This function is an RTOS task for attaching the USB Device to Host. Following
 are the actions done by this Task.
 1) Open an instance of Harmony USB Device Framework by periodically calling
    (in every 1 milli Second) USB_DEVICE_Open()function until Harmony USB Device
     framework is successfully opened.
 2) If the USB Device Framework is opened successfully pass an application event
    Handler to the USB framework for receiving USB Device Events.
 3) Attach to the USB Host by calling USB attach function.
 4) Acquire a binary semaphore to wait until USB Host Configures the Device. The
    semaphore is released when a USB_DEVICE_EVENT_CONFIGURED event is received at
    the USB Device event handler.
 5) Resume all CDC read/write tasks.
 6) Suspend USB attach task.

  Returns:
     None
*/
void USBDevice_Task(void* p_arg)
{
    uint32_t USBDeviceTask_State = USBDEVICETASK_OPENUSB_STATE;
    USBCDC_intern USBDeviceTask_Event;
    USBDeviceTask_Event.data = 0;
    USBDeviceTask_Event.size = 0;
    USB_DEVICE_CDC_TRANSFER_HANDLE USBCDC_Read_Handle, USBCDC_Write_Handle;
    USBCDC_Read_Handle = USB_DEVICE_CDC_TRANSFER_HANDLE_INVALID;
    USBCDC_Write_Handle = USB_DEVICE_CDC_TRANSFER_HANDLE_INVALID;
    SERVO_POSITIONS_TYPE servoPosTMRticks;
    uint8_t ui8;
    int length;
    uint16_t ui16TempServoPosTMRticks;
    
    // The added space (HEADERCOUNT + BYTESPERCHANNEL*CHANNELCOUNT - 1) is for saving possible leftovers of previous packet.
    uint8_t ReadProcessBuffer[USBCDC_BUFFER_SIZE + HEADERCOUNT + BYTESPERCHANNEL*CHANNELCOUNT - 1] ;
    uint16_t ui16ReadProcessBufCount = 0;
    uint16_t ui16ReadProcessBufPointer;
#ifdef FREERTOSSTATISTICS
    static bool bWriteDone = false;
#endif    

    for(;;)
    {
        switch(USBDeviceTask_State)
        {
            case USBDEVICETASK_OPENUSB_STATE:
                usbcdcData.deviceHandle = USB_DEVICE_Open( USB_DEVICE_INDEX_0, DRV_IO_INTENT_READWRITE );
                /*do we have access to USB, if not try again*/
                if(usbcdcData.deviceHandle != USB_DEVICE_HANDLE_INVALID)
                {
                    //USBDeviceTask_State = USBDEVICETASK_PROCESSUSBEVENTS_STATE;
                    USB_DEVICE_EventHandlerSet(usbcdcData.deviceHandle, APP_USBDeviceEventHandler, 0);
                    USBDeviceTask_State = USBDEVICETASK_ATTACHUSB_STATE;
                    break;
                }
                // try again in 10 msec
                USBDeviceTask_State = USBDEVICETASK_OPENUSB_STATE;
                vTaskDelay(10 / portTICK_PERIOD_MS);
                break;
            case USBDEVICETASK_ATTACHUSB_STATE: 
                USB_DEVICE_Attach (usbcdcData.deviceHandle);
                USBDeviceTask_State = USBDEVICETASK_PROCESSUSBEVENTS_STATE;
                break;
            case USBDEVICETASK_PROCESSUSBEVENTS_STATE:
                // once here, USB task becomes event driven, user input will will generate events
                USBDeviceTask_State = USBDEVICETASK_PROCESSUSBEVENTS_STATE;                

                // wait for an event to occur and process, see event handler
                if(xQueueReceive(qh_USBDeviceTask_EventQueue_Handle, &USBDeviceTask_Event, portMAX_DELAY) != pdPASS)
                    break;
                
                switch(USBDeviceTask_Event.data)
                {
                    case USBDEVICETASK_USBPOWERED_EVENT:
                        USB_DEVICE_Attach (usbcdcData.deviceHandle);
                        break;
                    case USBDEVICETASK_USBCONFIGURED_EVENT:
                        // USB ready, wait for user input on either com port

                        // Schedule a first read
                        if (USB_DEVICE_CDC_Read(USB_DEVICE_CDC_INDEX_0, &USBCDC_Read_Handle, USBCDC_ReadBuffer, USBCDC_BUFFER_SIZE) != USB_DEVICE_CDC_RESULT_OK)
                        {
                            LEDOn();
                            configASSERT(0);
                        }
                        break;                    
                    case USBDEVICETASK_READDONECOM_EVENT:
                        // If there was data coming in, copy it first to internal ReadProcessBuffer
                        if (USBDeviceTask_Event.size > 0)
                        {
                            memcpy(&ReadProcessBuffer[ui16ReadProcessBufCount], USBCDC_ReadBuffer, USBDeviceTask_Event.size);
                            ui16ReadProcessBufCount += USBDeviceTask_Event.size;
                        }
                        
                        // All further processing is done only with ReadProcessBuffer
                        
                        // Process as long as we have data in the buffer
                        ui16ReadProcessBufPointer = 0;
                        
                        while ((ui16ReadProcessBufCount - ui16ReadProcessBufPointer) >= HEADERCOUNT + BYTESPERCHANNEL*CHANNELCOUNT)
                        {
                            if ((ReadProcessBuffer[ui16ReadProcessBufPointer+0] == HEADER1) && (ReadProcessBuffer[ui16ReadProcessBufPointer+1] == HEADER2))
                            {
                                for (ui8 = 0; ui8 < CHANNELCOUNT; ui8++)
                                {
                                    ui16TempServoPosTMRticks = (256*(uint16_t)ReadProcessBuffer[ui16ReadProcessBufPointer+2*ui8+2]) + (uint16_t)ReadProcessBuffer[ui16ReadProcessBufPointer+2*ui8+1+2];
                                    if ((ui16TempServoPosTMRticks >= MINSERVOPOSITIONUS*TMRTICKSPERUS) && (ui16TempServoPosTMRticks <= MAXSERVOPOSITIONUS*TMRTICKSPERUS))
                                    {
                                        servoPosTMRticks.ui16_ServoPulseDurationTMRticks[ui8] = ui16TempServoPosTMRticks;
                                    } else
                                    {
                                        // Out of bounds
                                        if (ui16TempServoPosTMRticks < MINSERVOPOSITIONUS*TMRTICKSPERUS)
                                        {
                                            servoPosTMRticks.ui16_ServoPulseDurationTMRticks[ui8] = MINSERVOPOSITIONUS*TMRTICKSPERUS;
                                            // Signal out of range
//                                            LED_YellowOn();
                                        }
                                        if (ui16TempServoPosTMRticks > MAXSERVOPOSITIONUS*TMRTICKSPERUS)
                                        {
                                            servoPosTMRticks.ui16_ServoPulseDurationTMRticks[ui8] = MAXSERVOPOSITIONUS*TMRTICKSPERUS;
                                            // Signal out of range
//                                            LED_YellowOn();
                                        }
                                    }
                                }
                                
                                // As we are interested only in the latest servo position values, use xQueueOverwrite instead of xQueueSend. The queue depth is 1.
                                // xQueueOverwrite should always succeed as long as qh_SERVOPOSITIONS_Queue exists.
                                if (qh_SERVOPOSITIONS_Queue != NULL)
                                {
                                    if (xQueueOverwrite( qh_SERVOPOSITIONS_Queue, &servoPosTMRticks) != pdPASS)
                                    {
                                        LEDOn();
                                        configASSERT(0);
                                    }
                                }

                                // Reply data back in ASCII
                                length = sprintf(USBCDC_WriteBuffer, "Ch1: %.2f", (float)servoPosTMRticks.ui16_ServoPulseDurationTMRticks[0]/TMRTICKSPERUS);
                                for (ui8 = 1; ui8 < CHANNELCOUNT; ui8++)
                                {
                                    length += sprintf(&USBCDC_WriteBuffer[length], ", Ch%u: %.2f", ui8+1, (float)servoPosTMRticks.ui16_ServoPulseDurationTMRticks[ui8]/TMRTICKSPERUS);
                                }
                                length += sprintf(&USBCDC_WriteBuffer[length], "\r\n");
                                USB_DEVICE_CDC_Write(USB_DEVICE_CDC_INDEX_0, &USBCDC_Write_Handle, USBCDC_WriteBuffer, length, USB_DEVICE_CDC_TRANSFER_FLAGS_DATA_COMPLETE);
                                ui16ReadProcessBufPointer += HEADERCOUNT + BYTESPERCHANNEL*CHANNELCOUNT;
                            }
                            else
                            {
                                ui16ReadProcessBufPointer++;
                                continue;
                            }
                        }

                        // If there are remaining bytes, we should make sure they are at the front of the ReadProcessBuffer.
                        // We only need to copy if the ui16ReadProcessBufPointer is pointing above one telegram length mark, otherwise the partial telegram is already at the beginning of ReadProcessBuffer.
                        
                        if (ui16ReadProcessBufPointer < ui16ReadProcessBufCount)
                        {
                            // There were remaining bytes that were not processed.
                            
                            // Check if the ui16ReadProcessBufPointer points above a telegram mark.
                            if (ui16ReadProcessBufPointer >= HEADERCOUNT + BYTESPERCHANNEL*CHANNELCOUNT)
                            {
                                // Copy the unprocessed data into the front of the buffer.
                                memcpy(ReadProcessBuffer, &ReadProcessBuffer[ui16ReadProcessBufPointer], ui16ReadProcessBufCount-ui16ReadProcessBufPointer);
                                ui16ReadProcessBufCount = ui16ReadProcessBufCount-ui16ReadProcessBufPointer;
                            }
                        }
                        else
                        {
                            // No further bytes to process in ReadProcessBuffer, mark the buffer for full reuse
                            ui16ReadProcessBufCount = 0;
                        }
#ifdef FREERTOSSTATISTICS
                        // Trigger new writeout
                        bWriteDone = false;
#endif    
                        // Schedule a new CDC read
                        if (USB_DEVICE_CDC_Read(USB_DEVICE_CDC_INDEX_0, &USBCDC_Read_Handle, USBCDC_ReadBuffer, USBCDC_BUFFER_SIZE) != USB_DEVICE_CDC_RESULT_OK)
                        {
                            LEDOn();
                            configASSERT(0);
                        }
                        break;
                    case USBDEVICETASK_WRITEDONECOM_EVENT:
#ifdef FREERTOSSTATISTICS
                        if (!bWriteDone)
                        {
                            length = sprintf(USBCDC_WriteBuffer, "$$$ SHWM _SYS_Tasks: %lu\r\n", uxTaskGetStackHighWaterMark(th_SYS_Tasks));
                            length += sprintf(&USBCDC_WriteBuffer[length], "$$$ SHWM USB_Device_Tasks: %lu\r\n", uxTaskGetStackHighWaterMark(th_USBDevice_Task));
                            if (appData.th_APP_Tasks != NULL)
                            {
                                length += sprintf(&USBCDC_WriteBuffer[length], "$$$ SHWM APP_Tasks: %lu\r\n", uxTaskGetStackHighWaterMark(appData.th_APP_Tasks));
                            }
                            else
                            {
                                length += sprintf(&USBCDC_WriteBuffer[length], "$$$ SHWM APP_Tasks (killed): %lu\r\n", appData.AppStackHighWaterMark);
                            }
                            bWriteDone = true;
                            USB_DEVICE_CDC_Write(USB_DEVICE_CDC_INDEX_0, &USBCDC_Write_Handle, USBCDC_WriteBuffer, length, USB_DEVICE_CDC_TRANSFER_FLAGS_DATA_COMPLETE);
                        }
#endif
                        break;
                    default:
                        break;
                }
                break;
            default:
                break;
        }
    }
}

bool USBCDC_Initialize(void)
{
    usbcdcData.deviceHandle = USB_DEVICE_HANDLE_INVALID;
    usbcdcData.usbcdcPort.getLineCodingData.dwDTERate = 115200;
    usbcdcData.usbcdcPort.getLineCodingData.bDataBits = 8;
    usbcdcData.usbcdcPort.getLineCodingData.bParityType = 0;
    usbcdcData.usbcdcPort.getLineCodingData.bCharFormat = 0;

    qh_USBDeviceTask_EventQueue_Handle = xQueueCreate(15, sizeof(USBCDC_intern));

    if(qh_USBDeviceTask_EventQueue_Handle == NULL)
        return false;   // Could not create USBDeviceTask_EventQueue

    if (xTaskCreate((TaskFunction_t) USBDevice_Task, "USB Device Task", USBDEVICETASK_SIZE, NULL, USBDEVICETASK_PRIO, &th_USBDevice_Task) != pdPASS)
        return false;   // Could not create USBDevice_Task

    return true;
}

/* *****************************************************************************
 End of File
 */
