# USB2PPM
Interface to control a remote device (e.g. a drone) from the PC via a radio link.

The USB2PPM circuit gets the signal from PC over USB connection and converts it to Pulse-Position-Modulation (PPM) to be input e.g. over a trainer jack into a remote control transmitter.

<img src="/media/USB2PPM_top.jpg" alt="Top side of the PCB" height="300"/> <img src="/media/USB2PPM_bottom.jpg" alt="Bottom side of the PCB" height="300"/>

USB2PPM outputs standard trainer port signal. You will find Eagle schematic & board files, Microchip MPLAB X PIC32 Harmony project (including pre-built firmware HEX) and two Microsoft Visual Studio .NET C# projects (incl. pre-built Windows binary) to control the board.

<img src="/media/DemoGUI.png" alt="Demo GUI" height="375"/>

USB2PPM circuit connects via USB-port of the PC and communicates over virtual serial port. Where the control input stems, depends only on the software running on the PC that transmits this info to the USB2PPM device.

### Communication protocol

The communication protocol is kept very simple. The connection with USB2PPM is with 115200 baud, 8N1. 

USB2PPM expects a constant 2 byte header (`0xF0 0xC4`) followed by eight uint16 values, sent MSB first (big-endian). Each of these uint16 values corresponds to a servo channel, starting with servo 1.

In the present firmware, the uint16 values code the servo channel pulse width in 200 ns resolution. The minimum accepted value is 1 ms (meaning value 5000 in decimal or `0x1388` in hex) and the maximum accepted value is 2 ms (meaning value 10000 or `0x2710` in hex).  Inputs outside the limits are capped to the borders of these limits (e.g., if you accidentally send 15000, the PIC32 firmware limits this input to 10000).

The USB2PPM echos the accepted input commands in human readable form (ASCII) back. There is no need to read, parse or acknowledge this feedback.

When sending the servo control telegram, you also do not need to adhere to any timing constraints or intervals when sending the data - after the telegram with new values has been accepted by USB2PPM, it continues to send out servo pulses with the commanded data until a new telegram with updated values is sent to it.

#### Examples

1) all 8 channels centered (1.5 ms)

`0xF0 0xC4  0x1D 0x4C 0x1D 0x4C 0x1D 0x4C 0x1D 0x4C 0x1D 0x4C 0x1D 0x4C 0x1D 0x4C 0x1D 0x4C`

2) all 8 channels minimum (1 ms):

`0xF0 0xC4 0x13 0x88 0x13 0x88 0x13 0x88 0x13 0x88 0x13 0x88 0x13 0x88 0x13 0x88 0x13 0x88`

3) all 8 channels maximum (2 ms):

`0xF0 0xC4 0x27 0x10 0x27 0x10 0x27 0x10 0x27 0x10 0x27 0x10 0x27 0x10 0x27 0x10 0x27 0x10`

4) channel 7 at 1.7 ms, rest at center:

`0xF0 0xC4  0x1D 0x4C 0x1D 0x4C 0x1D 0x4C 0x1D 0x4C 0x1D 0x4C 0x1D 0x4C 0x21 0x34 0x1D 0x4C`

(1.7 ms in 200 ns steps is 8500 that is in hex '0x2134'. The value 8500 stems from equation:

`pulse_width / step_size`

and here with example values for 1.7ms pulse width: 1.7 * 1000 * 1000 / 200 = 8500.

The first 1000 in the multiplication is to get from milliseconds to microseconds and the second multiplication with 1000 to get from microsecons to nanoseconds.

### Discussion

Project discussion in RC Groups forum blog post:

<https://www.rcgroups.com/forums/showthread.php?3845229-Blog-10-USB2PPM-Interface-for-controlling-a-remote-device-from-PC>
