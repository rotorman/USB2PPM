This is the firmware source for the USB to PPM (Pulse Position Modulation) converter.
 
Code for USB2PPM v1.0 PCB with PIC32MX270F256D.
PPM output is over pin 43 / RB7.
  
Uses Output Compare (OC1) together with Timer 3 module.
Pulse generation happens in OC1 ISR ("IntHandlerDrvOCInstance0()" in system_interrupt.c).
The unit replies input in human readable form (the generated pulse lengths).

USB CDC communication with host: 115200 8N1, no flow.
It uses the Microchip Harmony v2.06 CDC single driver, found under default installation:
C:\microchip\harmony\v2_06\apps\usb\device\cdc_com_port_single\inf
 
Protocol from host: F0 C4 <Servo1, uint16 MSB first> ... <Servo 8>
Servo data is in 0.2 µs ticks, thus middle position 1500 µs = 7500 = 0x1D4C
Limits are 1 ms (5000 = 0x1388) and 2 ms (10000 = 0x2710).
Inputs outside the limits are capped to the borders of the limits.
The system confirms the accepted input commands in human readable form
(output is a ASCII line of the generated pulse lengths).

This code is tested with Microchip MPLAB X v5.45, XC32 v2.50 and Harmony v2.06.

= Setting up the build environment =

For downloading and setting up the programming environment, please see:
1) https://www.microchip.com/mplab/mplab-x-ide
2) https://www.microchip.com/mplab/compilers
3) https://www.microchip.com/mplab/mplab-harmony/mplab-harmony-v2

Please check out the code to your harmony folder structure under
\third_party\own\apps\USB2PPM

As an example, under Windows, the MPLAB project would need to go under:
C:\microchip\harmony\v2_06\third_party\own\apps\USB2PPM\firmware

The code is making use of FreeRTOS extensions for multi-tasking and thread safe communication.
https://www.freertos.org/
https://www.freertos.org/wp-content/uploads/2018/07/161204_Mastering_the_FreeRTOS_Real_Time_Kernel-A_Hands-On_Tutorial_Guide.pdf

Please note that unfortunately there is an important bug in the FreeRTOS version, shipping
with Harmony v2.06, causing the sprintf() to fail (sprintf() is used in this firmware).
Please see Richard Barry, the author of FreeRTOS, explain the problem and how to remedy this:
https://www.freertos.org/FreeRTOS_Support_Forum_Archive/November_2010/freertos_Bad_sprintf_behaviour_on_PIC32_3951286.html
In short, you need to modify a single line in the function pxPortInitialiseStack() in file:
\third_party\rtos\FreeRTOS\Source\portable\MPLAB\PIC32MX\port.c
The first pxTopOfStack decrementor needs to decrement 2 counts, not only one, thus change it to:
pxTopOfStack-=2;