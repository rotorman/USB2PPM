/* ************************************************************************** */
/** Descriptive File Name

  @Company
    Company Name

  @File Name
    filename.h

  @Summary
    Brief description of the file.

  @Description
    Describe the purpose of this file.
 */
/* ************************************************************************** */

#ifndef _USBCDC_H    /* Guard against multiple inclusion */
#define _USBCDC_H


/* ************************************************************************** */
/* ************************************************************************** */
/* Section: Included Files                                                    */
/* ************************************************************************** */
/* ************************************************************************** */

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdlib.h>
#include <stdio.h>
#include "system_config.h"
#include "system_definitions.h"
#include "FreeRTOS.h"
#include "semphr.h"

/* Provide C++ Compatibility */
#ifdef __cplusplus
extern "C" {
#endif

/* ************************************************************************** */
/* ************************************************************************** */
/* Section: Constants                                                         */
/* ************************************************************************** */
/* ************************************************************************** */

#define USBCDC_BUFFER_SIZE                      512u

// *****************************************************************************
/* USBCDC States

  Summary:
    Application states 

  Description:
    This defines the valid application states.  These states
    determine the behavior of the application at various times.
*/
#define USBDEVICETASK_OPENUSB_STATE             1
#define USBDEVICETASK_ATTACHUSB_STATE           2
#define USBDEVICETASK_PROCESSUSBEVENTS_STATE    3

#define USBDEVICETASK_USBPOWERED_EVENT          1
#define USBDEVICETASK_USBCONFIGURED_EVENT       2
#define USBDEVICETASK_READDONECOM_EVENT         3
#define USBDEVICETASK_WRITEDONECOM_EVENT        4

// *****************************************************************************
// *****************************************************************************
// Section: Free RTOS Task Priorities
// *****************************************************************************
// *****************************************************************************

#define  USBDEVICETASK_PRIO                     2u

// *****************************************************************************
// *****************************************************************************
// Section: Free RTOS Task Stack Sizes
// *****************************************************************************
// *****************************************************************************
#define  USBDEVICETASK_SIZE                     512u

// *****************************************************************************
// *****************************************************************************
// Section: Type Definitions
// *****************************************************************************
// *****************************************************************************

/******************************************************
 * USB CDC COM Port Object
 ******************************************************/

typedef struct
{
    /* CDC instance number */
    USB_DEVICE_CDC_INDEX cdcInstance;

    /* Set Line Coding Data */
    USB_CDC_LINE_CODING setLineCodingData;

    /* Get Line Coding Data */
    USB_CDC_LINE_CODING getLineCodingData;

    /* Control Line State */
    USB_CDC_CONTROL_LINE_STATE controlLineStateData;

    /* Break data */
    uint16_t breakData;

} USBCDC_PORT_OBJECT;

typedef struct
{
    /* Device layer handle returned by device layer open function */
    USB_DEVICE_HANDLE deviceHandle;

    USBCDC_PORT_OBJECT usbcdcPort;
} USBCDC_DATA;

//this struct is used intern to get not only the data but the number of received characters as well
typedef struct
{
    size_t size;
    uint32_t data;
} USBCDC_intern;

// *****************************************************************************
// *****************************************************************************
// Section: Interface Functions
// *****************************************************************************
// *****************************************************************************

bool USBCDC_Initialize(void);

/* Provide C++ Compatibility */
#ifdef __cplusplus
}
#endif

#endif /* _USBCDC_H */

/* *****************************************************************************
 End of File
 */
