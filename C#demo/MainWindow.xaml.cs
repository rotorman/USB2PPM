// *** USB2PPM Demo ***
//
// Last modified:
// 2020-11-09 Risto Kõiva

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;
using System.IO.Ports;
using System.ComponentModel;

namespace ServoControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Constant declarations
        private const string sCONNECT = "_Connect"; // Button content for unconnected state
        private const string sDISCONNECT = "_Disconnect"; // Button content for connected state
                                                          //private const int CQUEUESIZE = 1024 * 1024; // Receive buffer size (1 MByte)
        private const byte bHEADER1 = 0xF0;
        private const byte bHEADER2 = 0xC4;
        private const int HEADERLENGTH = 2;
        private const int CHANNELCOUNT = 8;
        private const int BYTESPERCHANNEL = 2;
        private const UInt16 DEFAULTSERVOPOSITION = 1500;   // 1,5 ms
        private const UInt16 MINSERVOPOSITIONUS = 1000;     // 1 ms
        private const UInt16 MAXSERVOPOSITIONUS = 2000;     // 2 ms
        private const UInt16 TIMERTICKSPERUS = 5;           // 5 hardware timer ticks per 1 µs, 200 ns resolution in hardware
        private const int SWEEPSTEP = 10;

        // Test result:
        // DJI Goggles RE and OcuSync Air Unit convert:
        // 1000 ms input -  885.0 ms SBUS output (according to RMILEC)
        // 1500 ms input - 1519.2 ms SBUS output (RMILEC)
        // 2000 ms input - 2115.5 ms SBUS output (RMILEC)
        // Channels 9 to 16 - 880.0 ms
        // Channels 17 & 18 - OFF
        // SBFS - OFF
        // SBRF - BUSY
        //
        // PPM cable disconnected from DJI Goggles RE - SBUS no signal (SBUS line constant low!)

        // Variable declarations
        private SerialPort spUSB = new SerialPort(); // Communication over (virtual) serial port 
        private delegate void WriteDataOutdelegate(byte[] baTelegram);

        private DispatcherTimer dtSweepTimer = new DispatcherTimer(); // Paces the autogen
        private bool bSweepDirectionUp = true;

        // MainWindow constructor gets called when the window is created - at the start of the program
        public MainWindow()
        {
            InitializeComponent();

            btConnectDisconnect.Content = sCONNECT; // Initialize the button content
            spUSB.DataReceived += new SerialDataReceivedEventHandler(spUSB_DataReceived); // Register new event for receiving serial data

            // Populate the combobox with available serial ports of the current system
            string[] saAvailableSerialPorts = SerialPort.GetPortNames();
            foreach (string sAvailableSerialPort in saAvailableSerialPorts)
                cbSerialPort.Items.Add(sAvailableSerialPort);
            if (cbSerialPort.Items.Count > 0)
            {
                cbSerialPort.Text = cbSerialPort.Items[0].ToString();
                for (int i = 0; i < cbSerialPort.Items.Count; i++)
                    if (cbSerialPort.Items[i].ToString() == "COM7") // If available, default to COM7 initially 
                        cbSerialPort.SelectedIndex = i;
            }
        }

        // btConnectDisconnect_Click gets calles each time a button on MainWindow is pressed
        private void btConnectDisconnect_Click(object sender, RoutedEventArgs e)
        {
            // Check according to button content, if the "Connect" or "Disconnect" was pressed
            if ((string)btConnectDisconnect.Content == sCONNECT)
            {
                // Connect was pressed

                // Set (virtual) serial port parameters
                spUSB.BaudRate = 115200; // 115.2 kbaud/s
                // spUSB.DataBits = 8; // default is 8
                // spUSB.DiscardNull = false; // default is false
                // spUSB.DtrEnable = false; // default is false
                // spUSB.Handshake = Handshake.None; // default is None
                // spUSB.Parity = Parity.None; // default is None
                spUSB.ParityReplace = 63;
                spUSB.PortName = cbSerialPort.SelectedItem.ToString();
                // spUSB.ReadBufferSize = 4096; // Default 4096
                // spUSB.ReadTimeout = -1; // -1 = default = InfiniteTimeout
                // spUSB.ReceivedBytesThreshold = 1; // Fire receive event when this amount of data is available, Default = 1
                spUSB.RtsEnable = true;
                //spUSB.StopBits = StopBits.One; // One is default
                //spUSB.WriteBufferSize = 2048; // Default 2048
                //spUSB.WriteTimeout = -1; // -1 = default = InfiniteTimeout

                try
                {
                    if (!spUSB.IsOpen)
                        spUSB.Open(); // Try to connect to the selected serial port

                    // If we got here, then the port was successfully opened, continue
                    btConnectDisconnect.Content = sDISCONNECT; // Change button content to disconnect string
                    cbSerialPort.IsEnabled = false; // Disable port selection combobox

                    WriteDataOut();
                }
                catch (Exception ex)
                {
                    // In case of error, show message to user
                    MessageBox.Show("Could not open the communication port!" + "\n" + "Error: " + ex.Message);
                }
            }
            else
            {
                // Disconnect was pressed
                try
                {
                    if (spUSB.IsOpen)
                        spUSB.Close(); // Try to stop serial port communications

                    cbSerialPort.IsEnabled = true; // Enable the port selection combobox
                    btConnectDisconnect.Content = sCONNECT; // Change button content to connect string
                }
                catch (Exception ex)
                {
                    // In case of error, show message to user
                    MessageBox.Show("Could not close the communication port!" + "\n" + "Error: " + ex.Message);
                }
                slider_ch1.Value = DEFAULTSERVOPOSITION;
                slider_ch2.Value = DEFAULTSERVOPOSITION;
                slider_ch3.Value = DEFAULTSERVOPOSITION;
                slider_ch4.Value = DEFAULTSERVOPOSITION;
                slider_ch5.Value = DEFAULTSERVOPOSITION;
                slider_ch6.Value = DEFAULTSERVOPOSITION;
                slider_ch7.Value = DEFAULTSERVOPOSITION;
                slider_ch8.Value = DEFAULTSERVOPOSITION;

                if (cbSweep.IsChecked == true)
                    cbSweep.IsChecked = false;
            }
        }

        // spUSB_DataReceived event gets called when data from (virtual) serial port is received.
        // Note! This function runs in a different thread as the MainWindow elements, thus care (for example
        // locking) needs to be taken in interacting with MainWindow elements.
        // It collects the data from serial port and sends them to a shared queue (FIFO).
        private void spUSB_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int iDataLength = spUSB.BytesToRead; // The amount of bytes, serial port has received
            byte[] baSerialPortDataIn = new byte[iDataLength]; // Create an internal array to store the data
            if (iDataLength > 0) // Makes sense to continue only, if there really is new data available
            {
                spUSB.Read(baSerialPortDataIn, 0, iDataLength); // Copy the data from serial port to internal array
                // Discard the data
            }
        }

        private void WriteDataOut()
        {
            byte[] baTelegram = new byte[HEADERLENGTH + BYTESPERCHANNEL * CHANNELCOUNT];
            UInt16[] ui16ServoPos = new UInt16[CHANNELCOUNT];

            if (slider_ch1 != null) ui16ServoPos[0] = (UInt16)(slider_ch1.Value * (double)TIMERTICKSPERUS); else ui16ServoPos[0] = DEFAULTSERVOPOSITION * TIMERTICKSPERUS;
            if (slider_ch2 != null) ui16ServoPos[1] = (UInt16)(slider_ch2.Value * (double)TIMERTICKSPERUS); else ui16ServoPos[1] = DEFAULTSERVOPOSITION * TIMERTICKSPERUS;
            if (slider_ch3 != null) ui16ServoPos[2] = (UInt16)(slider_ch3.Value * (double)TIMERTICKSPERUS); else ui16ServoPos[2] = DEFAULTSERVOPOSITION * TIMERTICKSPERUS;
            if (slider_ch4 != null) ui16ServoPos[3] = (UInt16)(slider_ch4.Value * (double)TIMERTICKSPERUS); else ui16ServoPos[3] = DEFAULTSERVOPOSITION * TIMERTICKSPERUS;
            if (slider_ch5 != null) ui16ServoPos[4] = (UInt16)(slider_ch5.Value * (double)TIMERTICKSPERUS); else ui16ServoPos[4] = DEFAULTSERVOPOSITION * TIMERTICKSPERUS;
            if (slider_ch6 != null) ui16ServoPos[5] = (UInt16)(slider_ch6.Value * (double)TIMERTICKSPERUS); else ui16ServoPos[5] = DEFAULTSERVOPOSITION * TIMERTICKSPERUS;
            if (slider_ch7 != null) ui16ServoPos[6] = (UInt16)(slider_ch7.Value * (double)TIMERTICKSPERUS); else ui16ServoPos[6] = DEFAULTSERVOPOSITION * TIMERTICKSPERUS;
            if (slider_ch8 != null) ui16ServoPos[7] = (UInt16)(slider_ch8.Value * (double)TIMERTICKSPERUS); else ui16ServoPos[7] = DEFAULTSERVOPOSITION * TIMERTICKSPERUS;

            baTelegram[0] = bHEADER1;
            baTelegram[1] = bHEADER2;

            for (int i = 0; i < CHANNELCOUNT; i++)
            {
                //ui16ServoPos[i] *= TIMERTICKSPERUS;   // Is already multiplied above
                baTelegram[2 + 2 * i] = (byte)(ui16ServoPos[i] >> 8);
                baTelegram[3 + 2 * i] = (byte)(ui16ServoPos[i] & 0x00FF);
            }

            try
            {
                if (spUSB.IsOpen)
                {
                    spUSB.Write(baTelegram, 0, HEADERLENGTH + BYTESPERCHANNEL * CHANNELCOUNT);
                }
            }
            catch (Exception ex)
            {
                // In case of error, show message to user
                MessageBox.Show("Could not write data out the communication port!" + "\n" + "Error: " + ex.Message);
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            WriteDataOut();               
        }

        private void cbSweep_Checked(object sender, RoutedEventArgs e)
        {
            slider_ch1.Value = DEFAULTSERVOPOSITION;
            slider_ch2.Value = DEFAULTSERVOPOSITION;
            slider_ch3.Value = DEFAULTSERVOPOSITION;
            slider_ch4.Value = DEFAULTSERVOPOSITION;
            slider_ch5.Value = DEFAULTSERVOPOSITION;
            slider_ch6.Value = DEFAULTSERVOPOSITION;
            slider_ch7.Value = DEFAULTSERVOPOSITION;
            slider_ch8.Value = DEFAULTSERVOPOSITION;

            dtSweepTimer.Tick += new EventHandler(dtSweepTimer_Tick);   // Register new event for timer to fire
            dtSweepTimer.Interval = new TimeSpan(0, 0, 0, 0, 20);       // Set timer interval to 20ms evaluating to 50 Hz frame update rate
            dtSweepTimer.Start();
        }

        private void cbSweep_Unchecked(object sender, RoutedEventArgs e)
        {
            dtSweepTimer.Stop(); // Stop the AutoGen timer
        }

        // dtSweepTimer_Tick event gets called when the dtAutoGenTimer timer fires.
        private void dtSweepTimer_Tick(object sender, EventArgs e)
        {

            if (bSweepDirectionUp)
            {
                // up
                slider_ch1.Value = Math.Min(MAXSERVOPOSITIONUS, slider_ch1.Value + SWEEPSTEP);
                slider_ch2.Value = Math.Min(MAXSERVOPOSITIONUS, slider_ch2.Value + SWEEPSTEP);
                slider_ch3.Value = Math.Min(MAXSERVOPOSITIONUS, slider_ch3.Value + SWEEPSTEP);
                slider_ch4.Value = Math.Min(MAXSERVOPOSITIONUS, slider_ch4.Value + SWEEPSTEP);
                slider_ch5.Value = Math.Min(MAXSERVOPOSITIONUS, slider_ch5.Value + SWEEPSTEP);
                slider_ch6.Value = Math.Min(MAXSERVOPOSITIONUS, slider_ch6.Value + SWEEPSTEP);
                slider_ch7.Value = Math.Min(MAXSERVOPOSITIONUS, slider_ch7.Value + SWEEPSTEP);
                slider_ch8.Value = Math.Min(MAXSERVOPOSITIONUS, slider_ch8.Value + SWEEPSTEP);

                if (slider_ch1.Value == MAXSERVOPOSITIONUS)
                {
                    // Change direction
                    bSweepDirectionUp = false;
                }
            }
            else
            {
                // down
                slider_ch1.Value = Math.Max(MINSERVOPOSITIONUS, slider_ch1.Value - SWEEPSTEP);
                slider_ch2.Value = Math.Max(MINSERVOPOSITIONUS, slider_ch2.Value - SWEEPSTEP);
                slider_ch3.Value = Math.Max(MINSERVOPOSITIONUS, slider_ch3.Value - SWEEPSTEP);
                slider_ch4.Value = Math.Max(MINSERVOPOSITIONUS, slider_ch4.Value - SWEEPSTEP);
                slider_ch5.Value = Math.Max(MINSERVOPOSITIONUS, slider_ch5.Value - SWEEPSTEP);
                slider_ch6.Value = Math.Max(MINSERVOPOSITIONUS, slider_ch6.Value - SWEEPSTEP);
                slider_ch7.Value = Math.Max(MINSERVOPOSITIONUS, slider_ch7.Value - SWEEPSTEP);
                slider_ch8.Value = Math.Max(MINSERVOPOSITIONUS, slider_ch8.Value - SWEEPSTEP);

                if (slider_ch1.Value == MINSERVOPOSITIONUS)
                {
                    // Change direction
                    bSweepDirectionUp = true;
                }
            }
        }

        private void slider_ch1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch1.Value = DEFAULTSERVOPOSITION;
        }

        private void slider_ch2_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch2.Value = DEFAULTSERVOPOSITION;
        }

        private void slider_ch3_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch3.Value = DEFAULTSERVOPOSITION;
        }

        private void slider_ch4_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch4.Value = DEFAULTSERVOPOSITION;
        }

        private void slider_ch5_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch5.Value = DEFAULTSERVOPOSITION;
        }

        private void slider_ch6_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch6.Value = DEFAULTSERVOPOSITION;
        }

        private void slider_ch7_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch7.Value = DEFAULTSERVOPOSITION;
        }

        private void slider_ch8_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch8.Value = DEFAULTSERVOPOSITION;
        }
    }
}
