using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.IO.Ports;
using Windows.Gaming.Input;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;

namespace SteeringWheelDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Constant declarations
        IReadOnlyList<RawGameController> controllers = RawGameController.RawGameControllers;

        private const string sCONNECT = "_Connect"; // Button content for unconnected state
        private const string sDISCONNECT = "_Disconnect"; // Button content for connected state
                                                          //private const int CQUEUESIZE = 1024 * 1024; // Receive buffer size (1 MByte)
        private const byte bHEADER = 0x0F;
        private const byte bFOOTER = 0x00;
        private const int CHANNELCOUNT = 16;
        private const UInt16 DEFAULTPOSITION = 1024;   // 1.5 ms
        private const UInt16 MINPOSITION = 144;        // ca. 1 ms
        private const UInt16 MAXPOSITION = 1904;       // ca. 2 ms
        private const UInt16 CENTERPOSITION = 1024;
        private const UInt16 WHEELDIFFERENCE = (MAXPOSITION - MINPOSITION) / 2;
        private const UInt16 PADELDIFFERENCE = MAXPOSITION - MINPOSITION;
        private const int SBUS_BAUDRATE = 100000;
        private const int SBUS_DATABITS = 8;
        private const StopBits SBUS_STOPBITS = StopBits.Two;

        private const UInt16 FILEHEADER = 0xaabb;
        private const UInt16 FILEFOOTER = 0xccdd;

        private int ErrCount = 1;

        // Variable declarations
        private SerialPort spUSB = new SerialPort(); // Communication over (virtual) serial port 
        private delegate void WriteDataOutdelegate(byte[] baTelegram);

        private DispatcherTimer dtWheelTimer = new DispatcherTimer();
        private DispatcherTimer dtSBUSTimer = new DispatcherTimer();

        // MainWindow constructor gets called when the window is created - at the start of the program
        public MainWindow()
        {
            InitializeComponent();
            btConnectDisconnect.Content = sCONNECT; // Initialize the button content
            // Remove below commant if you want to receive the data and write it into the binary file.
            //spUSB.DataReceived += new SerialDataReceivedEventHandler(spUSB_DataReceived); // Register new event for receiving serial data
            AddDevices();
        }

        // btConnectDisconnect_Click gets calles each time a button on MainWindow is pressed
        private void btConnectDisconnect_Click(object sender, RoutedEventArgs e)
        {
            // Check according to button content, if the "Connect" or "Disconnect" was pressed
            if ((string)btConnectDisconnect.Content == sCONNECT)
            {
                // Connect was pressed

                // Set (virtual) serial port parameters
                // S.BUS is 100.000 baud 8E2
                spUSB.BaudRate = SBUS_BAUDRATE;
                spUSB.DataBits = SBUS_DATABITS; // default is 8
                // spUSB.DiscardNull = false; // default is false
                // spUSB.DtrEnable = false; // default is false
                // spUSB.Handshake = Handshake.None; // default is None
                spUSB.Parity = Parity.Even; // default is None
                spUSB.ParityReplace = 63;
                spUSB.PortName = cbSerialPort.SelectedItem.ToString();
                // spUSB.ReadBufferSize = 4096; // Default 4096
                // spUSB.ReadTimeout = -1; // -1 = default = InfiniteTimeout
                // spUSB.ReceivedBytesThreshold = 1; // Fire receive event when this amount of data is available, Default = 1
                spUSB.RtsEnable = true;
                spUSB.StopBits = SBUS_STOPBITS; // One is default
                //spUSB.WriteBufferSize = 2048; // Default 2048
                //spUSB.WriteTimeout = -1; // -1 = default = InfiniteTimeout

                try
                {
                    if (!spUSB.IsOpen)
                        spUSB.Open(); // Try to connect to the selected serial port

                    // If we got here, then the port was successfully opened, continue
                    btConnectDisconnect.Content = sDISCONNECT; // Change button content to disconnect string
                    cbSerialPort.IsEnabled = false; // Disable port selection combobox

                    dtSBUSTimer.Tick += new EventHandler(dtSBUSTimer_Tick);   // Register new event for timer to fire
                    dtSBUSTimer.Interval = new TimeSpan(0, 0, 0, 0, 7);       // Set timer interval to 7ms evaluating to ca. 143 Hz frame update rate
                    dtSBUSTimer.Start();
                }
                catch (Exception ex)
                {
                    // In case of error, show message to user
                    OutputList.Items.Add(ErrCount++.ToString() + "Could not open the communication port!" + "\n" + "Error: " + ex.Message);
                }
            }
            else
            {
                // Disconnect was pressed
                dtSBUSTimer.Stop();

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
                    OutputList.Items.Add(ErrCount++.ToString() + "Could not close the communication port!" + "\n" + "Error: " + ex.Message);
                }
                slider_ch1.Value = DEFAULTPOSITION;
                slider_ch2.Value = MINPOSITION;
                slider_ch3.Value = MINPOSITION;

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
            try
            {
                if (iDataLength > 0) // Makes sense to continue only, if there really is new data available
                {
                    spUSB.Read(baSerialPortDataIn, 0, iDataLength); // Copy the data from serial port to internal array

                    // Write the received data into binary file.
                    using (var stream = new FileStream("SerialReceivedData.bin", FileMode.Append, FileAccess.Write, FileShare.None))
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(FILEHEADER);
                        writer.Write(baSerialPortDataIn);
                        writer.Write(FILEFOOTER);
                    }
                }
            }
            catch (Exception ex)
            {
                // In case of error, show message to user
                OutputList.Items.Add(ErrCount++.ToString() + "Could not receive the data from the serial port of could not write data into file!" + "\n" + "Error: " + ex.Message);
            }
        }

        public static byte[] SbusWriteChannels(ushort[] ch_)
        {
            byte[] buf_ = new byte[25];
            buf_[0] = bHEADER;
            buf_[1] = (byte)(ch_[0] & 0x07FF);
            buf_[2] = (byte)((ch_[0] & 0x07FF) >> 8 | (ch_[1] & 0x07FF) << 3);
            buf_[3] = (byte)((ch_[1] & 0x07FF) >> 5 | (ch_[2] & 0x07FF) << 6);
            buf_[4] = (byte)((ch_[2] & 0x07FF) >> 2);
            buf_[5] = (byte)((ch_[2] & 0x07FF) >> 10 | (ch_[3] & 0x07FF) << 1);
            buf_[6] = (byte)((ch_[3] & 0x07FF) >> 7 | (ch_[4] & 0x07FF) << 4);
            buf_[7] = (byte)((ch_[4] & 0x07FF) >> 4 | (ch_[5] & 0x07FF) << 7);
            buf_[8] = (byte)((ch_[5] & 0x07FF) >> 1);
            buf_[9] = (byte)((ch_[5] & 0x07FF) >> 9 | (ch_[6] & 0x07FF) << 2);
            buf_[10] = (byte)((ch_[6] & 0x07FF) >> 6 | (ch_[7] & 0x07FF) << 5);
            buf_[11] = (byte)((ch_[7] & 0x07FF) >> 3);
            buf_[12] = (byte)(ch_[8] & 0x07FF);
            buf_[13] = (byte)((ch_[8] & 0x07FF) >> 8 | (ch_[9] & 0x07FF) << 3);
            buf_[14] = (byte)((ch_[9] & 0x07FF) >> 5 | (ch_[10] & 0x07FF) << 6);
            buf_[15] = (byte)((ch_[10] & 0x07FF) >> 2);
            buf_[16] = (byte)((ch_[10] & 0x07FF) >> 10 | (ch_[11] & 0x07FF) << 1);
            buf_[17] = (byte)((ch_[11] & 0x07FF) >> 7 | (ch_[12] & 0x07FF) << 4);
            buf_[18] = (byte)((ch_[12] & 0x07FF) >> 4 | (ch_[13] & 0x07FF) << 7);
            buf_[19] = (byte)((ch_[13] & 0x07FF) >> 1);
            buf_[20] = (byte)((ch_[13] & 0x07FF) >> 9 | (ch_[14] & 0x07FF) << 2);
            buf_[21] = (byte)((ch_[14] & 0x07FF) >> 6 | (ch_[15] & 0x07FF) << 5);
            buf_[22] = (byte)((ch_[15] & 0x07FF) >> 3);
            buf_[23] = 0x00;
            buf_[24] = bFOOTER;
            return buf_;
        }

        private void dtSBUSTimer_Tick(object sender, EventArgs e)
        {
            UInt16[] ui16ServoPos = new UInt16[CHANNELCOUNT];

            if (slider_ch1 != null) ui16ServoPos[0] = (UInt16)(slider_ch1.Value); else ui16ServoPos[0] = DEFAULTPOSITION;
            if (slider_ch2 != null) ui16ServoPos[1] = (UInt16)(slider_ch2.Value); else ui16ServoPos[1] = MINPOSITION;
            if (slider_ch3 != null) ui16ServoPos[2] = (UInt16)(slider_ch3.Value); else ui16ServoPos[2] = MINPOSITION;

            // mix throttle & brake into ch4
            if ((slider_ch2 != null) && (slider_ch3 != null))
            {
                ui16ServoPos[3] = (UInt16)(((slider_ch3.Value - MINPOSITION) / 2) - (((slider_ch2.Value) - MINPOSITION) / 2) + DEFAULTPOSITION);
            }
            else
                ui16ServoPos[3] = DEFAULTPOSITION;

            // mix throttle & brake into ch5 with brake priority
            if ((slider_ch2 != null) && (slider_ch3 != null))
            {
                if (slider_ch2.Value > MINPOSITION)
                {
                    ui16ServoPos[4] = (UInt16)(-((slider_ch2.Value - MINPOSITION) / 2) + DEFAULTPOSITION);
                }
                else
                {
                    ui16ServoPos[4] = (UInt16)(((slider_ch3.Value - MINPOSITION) / 2) + DEFAULTPOSITION);
                }
            }
            else
                ui16ServoPos[4] = DEFAULTPOSITION;


            for (int i = 5; i < CHANNELCOUNT; i++)
            {
                ui16ServoPos[i] = DEFAULTPOSITION;
            }

            try
            {
                byte[] data = SbusWriteChannels(ui16ServoPos);

                if (spUSB.IsOpen)
                {
                    spUSB.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                // In case of error, show message to user
                OutputList.Items.Add(ErrCount++.ToString() + ". " + "Could not write data out the communication port!" + "\n" + "Error: " + ex.Message);
            }
        }

        private void slider_ch1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch1.Value = DEFAULTPOSITION;
        }

        private void slider_ch2_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch2.Value = MINPOSITION;
        }

        private void slider_ch3_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch3.Value = MINPOSITION;
        }

        private void btRefresh_Click(object sender, RoutedEventArgs e)
        {
            AddDevices();
        }

        private void AddDevices()
        {
            cbWheel.Items.Clear();
            cbSerialPort.Items.Clear();

            // Populate the combobox with available serial ports and steering wheel of the current system
            DisplayRadio();
            DisplayWheel();
        }

        private void DisplayRadio()
        {
            try
            {
                foreach (string sAvailableSerialPort in SerialPort.GetPortNames())
                {
                    cbSerialPort.Items.Add(sAvailableSerialPort);
                }

                if (cbSerialPort.Items.Count > 0)
                {
                    cbSerialPort.Text = cbSerialPort.Items[0].ToString();
                }
            }
            catch (Exception ex)
            {
                OutputList.Items.Add(ErrCount++.ToString() + ". " + ex.Message);
            }
        }

        private void DisplayWheel()
        {
            try
            {
                checkbWheel.IsEnabled = false;
                controllers = RawGameController.RawGameControllers;
                if (controllers.Any())
                {
                    foreach (var sAvailableWheel in controllers)
                    {
                        cbWheel.Items.Add(sAvailableWheel);
                    }
                }

                if (cbWheel.Items.Count > 0)
                {
                    checkbWheel.IsEnabled = true;
                    cbWheel.Text = cbWheel.Items[0].ToString();
                }
            }
            catch (Exception ex)
            {
                OutputList.Items.Add(ErrCount++.ToString() + ". " + ex.Message);
            }
        }

        private void btReadWheelData(object sender, RoutedEventArgs e)
        {
            RacingWheel racingWheel = RacingWheel.FromGameController(RawGameController.RawGameControllers[cbWheel.SelectedIndex]);
            RacingWheelReading wheel = racingWheel.GetCurrentReading();
            OutputList.Items.Add("wheel - " + wheel.Wheel.ToString());
            OutputList.Items.Add("Break - " + wheel.Brake.ToString());
            OutputList.Items.Add("Throttle - " + wheel.Throttle.ToString());
            OutputList.Items.Add("----------------------");
        }

        private void checkbWheel_Checked(object sender, RoutedEventArgs e)
        {
            slider_ch1.Value = DEFAULTPOSITION;
            slider_ch2.Value = MINPOSITION;
            slider_ch3.Value = MINPOSITION;

            dtWheelTimer.Tick += new EventHandler(dtWheelTimer_Tick);   // Register new event for timer to fire
            dtWheelTimer.Interval = new TimeSpan(0, 0, 0, 0, 20);       // Set timer interval to 20ms evaluating to 50 Hz frame update rate
            dtWheelTimer.Start();
        }

        private void checkbWheel_Unchecked(object sender, RoutedEventArgs e)
        {
            dtWheelTimer.Stop();
        }

        private void dtWheelTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                RacingWheel racingWheel = RacingWheel.FromGameController(RawGameController.RawGameControllers[cbWheel.SelectedIndex]);
                if (racingWheel != null)
                {
                    RacingWheelReading wheel = racingWheel.GetCurrentReading();

                    // Assuming input wheel value is between -1 and +1, then the following converts it to 144..1904 for S.BUS
                    slider_ch1.Value = (UInt16)(CENTERPOSITION + WHEELDIFFERENCE * wheel.Wheel);
                    // Assuming input break and throttle value is between 0 and +1, then the following converts it to 144..1904 for S.BUS
                    slider_ch2.Value = (UInt16)(MINPOSITION + PADELDIFFERENCE * wheel.Brake);
                    slider_ch3.Value = (UInt16)(MINPOSITION + PADELDIFFERENCE * wheel.Throttle);
                }
            }
            catch (Exception ex)
            {
                dtWheelTimer.Stop();
                OutputList.Items.Add(ErrCount++.ToString() + ". " + "There is a problem with connecting with steering wheel because of the following reason - " + ex.Message);
            }
        }
    }
}
