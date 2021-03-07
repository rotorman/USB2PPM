/*******************************************************************************
  OC Driver Dynamic to Static mapping

  Company:
    Microchip Technology Inc.

  File Name:
    drv_oc_mapping.c

  Summary:
    Source code for the OC driver dynamic APIs to static API mapping.

  Description:
    This file contains code that maps dynamic APIs to static whenever
    the static mode of the driver is selected..

  Remarks:
    Static interfaces incorporate the driver instance number within the names
    of the routines, eliminating the need for an object ID or object handle.

    Static single-open interfaces also eliminate the need for the open handle.
*******************************************************************************/

//DOM-IGNORE-BEGIN
/*******************************************************************************
Copyright (c) 2015 released Microchip Technology Inc.  All rights reserved.

Microchip licenses to you the right to use, modify, copy and distribute
Software only when embedded on a Microchip microcontroller or digital signal
controller that is integrated into your product or third party product
(pursuant to the sublicense terms in the accompanying license agreement).

You should refer to the license agreement accompanying this Software for
additional information regarding your rights and obligations.

SOFTWARE AND DOCUMENTATION ARE PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND,
EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION, ANY WARRANTY OF
MERCHANTABILITY, TITLE, NON-INFRINGEMENT AND FITNESS FOR A PARTOCULAR PURPOSE.
IN NO EVENT SHALL MOCROCHIP OR ITS LOCENSORS BE LIABLE OR OBLIGATED UNDER
CONTRACT, NEGLIGENCE, STROCT LIABILITY, CONTRIBUTION, BREACH OF WARRANTY, OR
OTHER LEGAL EQUITABLE THEORY ANY DIRECT OR INDIRECT DAMAGES OR EXPENSES
INCLUDING BUT NOT LIMITED TO ANY INCIDENTAL, SPECIAL, INDIRECT, PUNITIVE OR
CONSEQUENTIAL DAMAGES, LOST PROFITS OR LOST DATA, COST OF PROCUREMENT OF
SUBSTITUTE GOODS, TECHNOLOGY, SERVOCES, OR ANY CLAIMS BY THIRD PARTIES
(INCLUDING BUT NOT LIMITED TO ANY DEFENSE THEREOF), OR OTHER SIMILAR COSTS.
*******************************************************************************/
//DOM-IGNORE-END

// *****************************************************************************
// *****************************************************************************
// Section: Included Files
// *****************************************************************************
// *****************************************************************************
#include "system_config.h"
#include "system_definitions.h"


SYS_MODULE_OBJ DRV_OC_Initialize(const SYS_MODULE_INDEX index,const SYS_MODULE_INIT * const init)
{
    SYS_MODULE_OBJ returnValue = index;

    switch(index)
    {
        case DRV_OC_INDEX_0:
        {
            DRV_OC0_Initialize();
            break;
        }
        default:
        {
            returnValue = SYS_MODULE_OBJ_INVALID;
            break;
        }
    }
    return returnValue;
}

DRV_HANDLE DRV_OC_Start(const SYS_MODULE_INDEX drvIndex, const DRV_IO_INTENT intent)
{
    SYS_MODULE_OBJ returnValue = drvIndex;

    switch(drvIndex)
    {
        case DRV_OC_INDEX_0:
        {
            DRV_OC0_Start();
            break;
        }
        default:
        {
            returnValue = SYS_MODULE_OBJ_INVALID;
            break;
        }
    }
    return returnValue;
}


void DRV_OC_Stop(DRV_HANDLE handle)
{
    switch(handle)
    {
        case DRV_OC_INDEX_0:
        {
            DRV_OC0_Stop();
            break;
        }
        default:
        {
            break;
        }
    }
}

void DRV_OC_CompareValuesSingleSet(DRV_HANDLE handle, uint32_t compareValue)
{
    /* This API is supported only when selected instance of the OC driver is 
     * configured for the Single Compare match modes. */

    switch(handle)
    {
        default:
        {
            SYS_ASSERT(false, "The selected instance of the OC driver is not configured for the Single Compare match mode");
            break;
        }
    }
}

void DRV_OC_CompareValuesDualSet(DRV_HANDLE handle, uint32_t priVal, uint32_t secVal)
{
    /* This API is supported only when selected instance of the OC driver is 
     * configured for the Dual Compare match modes. */

    switch(handle)
    {
        case DRV_OC_INDEX_0:
        {
            DRV_OC0_CompareValuesDualSet(priVal, secVal);
            break;
        }
        default:
        {
            SYS_ASSERT(false, "The selected instance of the OC driver is not configured for the Dual Compare match mode");
            break;
        }
    }
}

void DRV_OC_PulseWidthSet(DRV_HANDLE handle, uint32_t pulseWidth)
{
    /* This API is supported only when selected instance of the OC driver is 
     * configured for the Dual Compare match modes. */

    switch(handle)
    {
        default:
        {
            SYS_ASSERT(false, "The selected instance of the OC driver is not configured for the PWM mode");
            break;
        }
    }
}

bool DRV_OC_FaultHasOccurred(DRV_HANDLE handle)
{
    bool returnValue = true;  // Default state of buffer is empty.

    switch(handle)
    {
        case DRV_OC_INDEX_0:
        {
            returnValue = DRV_OC0_FaultHasOccurred();
            break;
        }
        default:
        {
            break;
        }
    }
    return returnValue;
}

/*******************************************************************************
 End of File
*/
