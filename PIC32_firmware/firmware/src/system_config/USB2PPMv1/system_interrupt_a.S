/*******************************************************************************
 System Interrupt Source File

  File Name:
    sys_interrupt_a.S

  Summary:
    Raw ISR definitions.

  Description:
    This file contains a definitions of the raw ISRs required to support the 
    interrupt sub-system.
*******************************************************************************/

// DOM-IGNORE-BEGIN
/*******************************************************************************
Copyright (c) 2011-2014 released Microchip Technology Inc.  All rights reserved.

Microchip licenses to you the right to use, modify, copy and distribute
Software only when embedded on a Microchip microcontroller or digital signal
controller that is integrated into your product or third party product
(pursuant to the sublicense terms in the accompanying license agreement).

You should refer to the license agreement accompanying this Software for
additional information regarding your rights and obligations.

SOFTWARE AND DOCUMENTATION ARE PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND,
EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION, ANY WARRANTY OF
MERCHANTABILITY, TITLE, NON-INFRINGEMENT AND FITNESS FOR A PARTICULAR PURPOSE.
IN NO EVENT SHALL MICROCHIP OR ITS LICENSORS BE LIABLE OR OBLIGATED UNDER
CONTRACT, NEGLIGENCE, STRICT LIABILITY, CONTRIBUTION, BREACH OF WARRANTY, OR
OTHER LEGAL EQUITABLE THEORY ANY DIRECT OR INDIRECT DAMAGES OR EXPENSES
INCLUDING BUT NOT LIMITED TO ANY INCIDENTAL, SPECIAL, INDIRECT, PUNITIVE OR
CONSEQUENTIAL DAMAGES, LOST PROFITS OR LOST DATA, COST OF PROCUREMENT OF
SUBSTITUTE GOODS, TECHNOLOGY, SERVICES, OR ANY CLAIMS BY THIRD PARTIES
(INCLUDING BUT NOT LIMITED TO ANY DEFENSE THEREOF), OR OTHER SIMILAR COSTS.
 *******************************************************************************/
// DOM-IGNORE-END
/*
*********************************************************************************************************
*                                           INCLUDES
*********************************************************************************************************
*/
#include <xc.h>



#include "ISR_Support.h"

 


/* OC Instance 0 Interrupt */
   .extern  IntHandlerDrvOCInstance0

   .section	.vector_6,code, keep
   .equ     __vector_dispatch_6, IntVectorDrvOCInstance0
   .global  __vector_dispatch_6
   .set     nomicromips
   .set     noreorder
   .set     nomips16
   .set     noat
   .ent  IntVectorDrvOCInstance0

IntVectorDrvOCInstance0:
    la    $26,  _IntHandlerDrvOCInstance0         
    jr    $26
    nop
	.end	IntVectorDrvOCInstance0

   .section	.DrvOCInstance0_vector_text, code, keep
   .set     nomicromips
   .set     noreorder
   .set     nomips16
   .set     noat
   .ent  _IntHandlerDrvOCInstance0

_IntHandlerDrvOCInstance0:
    portSAVE_CONTEXT
    la    s6,  IntHandlerDrvOCInstance0
    jalr  s6
    nop
    portRESTORE_CONTEXT
    .end	_IntHandlerDrvOCInstance0



/* USB Device Interrupt */
   .extern  IntHandlerUSBInstance0

   .section	.vector_30,code, keep
   .equ     __vector_dispatch_30, IntVectorUSBInstance0
   .global  __vector_dispatch_30
   .set     nomicromips
   .set     noreorder
   .set     nomips16
   .set     noat
   .ent  IntVectorUSBInstance0

IntVectorUSBInstance0:
    la    $26,  _IntHandlerUSBInstance0         
    jr    $26
    nop
	.end	IntVectorUSBInstance0

   .section	.USBInstance0_vector_text, code, keep
   .set     nomicromips
   .set     noreorder
   .set     nomips16
   .set     noat
   .ent  _IntHandlerUSBInstance0

_IntHandlerUSBInstance0:
    portSAVE_CONTEXT
    la    s6,  IntHandlerUSBInstance0
    jalr  s6
    nop
    portRESTORE_CONTEXT
    .end	_IntHandlerUSBInstance0



/*******************************************************************************
 End of File
 */

