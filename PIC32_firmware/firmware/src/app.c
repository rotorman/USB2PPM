/*******************************************************************************
  USB to PPM (Pulse Position Modulation) converter
 
  Targets USB2PPM v1 PCB with PIC32MX270F256D
  PPM output on pin 43 / RB7.
  
  Uses Output Compare (OC1) together with Timer 3 module.
  Pulse generation happens in OC1 ISR ("IntHandlerDrvOCInstance0()" in system_interrupt.c).
  USB CDC communication with host. 115200 8N1, no flow.
 
  Protocol from host: F0 C4 <Servo1, uint16 MSB first> ... <Servo 8>
  Servo data is in 1/5 µs ticks, thus middle position 1500 µs = 7500 = 0x1D4C
  Limits are 1 ms (5000 = 0x1388) and 2 ms (10000 = 0x2710).
  Inputs outside the limits are capped to the borders of the limits.
  The system confirms the accepted input commands in human readable form
  (output is a ASCII line of the generated pulse lengths).
 *******************************************************************************/

// *****************************************************************************
// *****************************************************************************
// Section: Included Files 
// *****************************************************************************
// *****************************************************************************

#include "app.h"
#include "queue.h"

// *****************************************************************************
// *****************************************************************************
// Section: Global Data Definitions
// *****************************************************************************
// *****************************************************************************

QueueHandle_t qh_SERVOPOSITIONS_Queue = NULL;

// *****************************************************************************
/* Application Data

  Summary:
    Holds application data

  Description:
    This structure holds the application's data.

  Remarks:
    This structure should be initialized by the APP_Initialize function.
    
    Application strings and buffers are be defined outside this structure.
*/
APP_DATA appData;

// *****************************************************************************
// *****************************************************************************
// Section: Application Initialization and State Machine Functions
// *****************************************************************************
// *****************************************************************************

/*******************************************************************************
  Function:
    void APP_Initialize ( void )

  Remarks:
    See prototype in app.h.
 */

void APP_Initialize ( void )
{
    PPMOn();
    LEDOff();
    
#ifdef FREERTOSSTATISTICS
    appData.AppStackHighWaterMark = 0;
#endif    
    appData.th_APP_Tasks = NULL;

    if (!USBCDC_Initialize())
    {
        // Could not initialize USBCDC
        LEDOn();
        while(1);
    }

    qh_SERVOPOSITIONS_Queue = xQueueCreate( 1, sizeof( SERVO_POSITIONS_TYPE ) );
    if (qh_SERVOPOSITIONS_Queue == NULL)
    {
        // Could not create SERVOPOSITIONS_Queue
        LEDOn();
        configASSERT(0);
    }
    
    appData.CAPTURECOMPARE_timer_handle = DRV_TMR_Open ( DRV_TMR_INDEX_0, DRV_IO_INTENT_EXCLUSIVE );
    if ( DRV_HANDLE_INVALID == appData.CAPTURECOMPARE_timer_handle )
    {
        // Unable to open timer driver
        LEDOn();
        configASSERT(0);
    }

    // Place the App state machine in its initial state
    appData.state = APP_STATE_REGISTER_TIMER_ALARM;
}


/******************************************************************************
  Function:
    void APP_Tasks ( void )

  Remarks:
    See prototype in app.h.
 */
void APP_Tasks ( void )
{
    while(true)
    {
        /* Check the application's current state. */
        switch ( appData.state )
        {
            /* Application's initial state. */
            case APP_STATE_REGISTER_TIMER_ALARM:
            {
                if(DRV_TMR_AlarmRegister ( appData.CAPTURECOMPARE_timer_handle, DEFAULTSERVOPOSITIONUS*TMRTICKSPERUS, true, 0, NULL ))   // PR3 = DEFAULTSERVOPOSITIONUS*TMRTICKSPERUS
                {
                    appData.state = APP_STATE_START_PULSE_OUTPUT;
                } 
                else
                {
                    LEDOn();
                    configASSERT(0);
                    while(1);
                }
            } break;

            case APP_STATE_START_PULSE_OUTPUT:
            {
                DRV_OC0_CompareValuesDualSet((LOWPULSEDURATIONUS*TMRTICKSPERUS)-FIXCOMPAREMISSINGTICK, (DEFAULTSERVOPOSITIONUS*TMRTICKSPERUS)-FIXCOMPAREMISSINGTICK);
        
                if (DRV_TMR_Start (appData.CAPTURECOMPARE_timer_handle))
                {
                    DRV_OC0_Start();
                    appData.state = APP_STATE_GATHER_SAMPLES;
                }
                else
                {
                    LEDOn();
                    configASSERT(0);
                    while(1);
                }
            } break;
        
            case APP_STATE_GATHER_SAMPLES:
            {
#ifdef FREERTOSSTATISTICS
                // Before killing the task save the max stack usage
                appData.AppStackHighWaterMark = uxTaskGetStackHighWaterMark(appData.th_APP_Tasks);
#endif
                appData.th_APP_Tasks = NULL;
                // Terminate this task (NULL = present task). All processing happens in the Interrupt Service Routines from now on.                
                vTaskDelete(NULL);
            } break;

            default:                            // The default state should never be executed
            {
                LEDOn();
                configASSERT(0);
                break;
            }
        }
    }
}
 

/*******************************************************************************
 End of File
 */


