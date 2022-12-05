// *** S.BUS out demo ***
//
// Last modified:
// 2022-12-05 Risto Kõiva

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.IO.Ports;

namespace SBUSout
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
        private const byte bHEADER = 0x0F;
        private const byte bFOOTER = 0x00;
        private const int CHANNELCOUNT = 16;
        private const UInt16 DEFAULTPOSITION = 1024;   // 1,52 ms
        private const UInt16 MINPOSITION = 144;        // ca. 1 ms
        private const UInt16 MAXPOSITION = 1904;       // ca. 2 ms
        private const int SWEEPSTEP = 10;

        // Variable declarations
        private SerialPort spUSB = new SerialPort(); // Communication over (virtual) serial port 
        private delegate void WriteDataOutdelegate(byte[] baTelegram);

        private DispatcherTimer dtSweepTimer = new DispatcherTimer(); // Paces the autogen
        private DispatcherTimer dtSBUSTimer = new DispatcherTimer();
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
                // S.BUS is 100.000 baud 8E2
                spUSB.BaudRate = 100000; // 115.2 kbaud/s
                // spUSB.DataBits = 8; // default is 8
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
                spUSB.StopBits = StopBits.Two; // One is default
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
                    MessageBox.Show("Could not open the communication port!" + "\n" + "Error: " + ex.Message);
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
                    MessageBox.Show("Could not close the communication port!" + "\n" + "Error: " + ex.Message);
                }
                slider_ch1.Value = DEFAULTPOSITION;
                slider_ch2.Value = DEFAULTPOSITION;
                slider_ch3.Value = DEFAULTPOSITION;
                slider_ch4.Value = DEFAULTPOSITION;
                slider_ch5.Value = DEFAULTPOSITION;
                slider_ch6.Value = DEFAULTPOSITION;
                slider_ch7.Value = DEFAULTPOSITION;
                slider_ch8.Value = DEFAULTPOSITION;

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

        public static byte[] SbusWriteChannels(ushort[] ch_)
        {
            byte[] buf_ = new byte[25];
            buf_[0] = bHEADER;
            buf_[1] = (byte)((ch_[0] & 0x07FF));
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
            buf_[12] = (byte)((ch_[8] & 0x07FF));
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
            if (slider_ch2 != null) ui16ServoPos[1] = (UInt16)(slider_ch2.Value); else ui16ServoPos[1] = DEFAULTPOSITION;
            if (slider_ch3 != null) ui16ServoPos[2] = (UInt16)(slider_ch3.Value); else ui16ServoPos[2] = DEFAULTPOSITION;
            if (slider_ch4 != null) ui16ServoPos[3] = (UInt16)(slider_ch4.Value); else ui16ServoPos[3] = DEFAULTPOSITION;
            if (slider_ch5 != null) ui16ServoPos[4] = (UInt16)(slider_ch5.Value); else ui16ServoPos[4] = DEFAULTPOSITION;
            if (slider_ch6 != null) ui16ServoPos[5] = (UInt16)(slider_ch6.Value); else ui16ServoPos[5] = DEFAULTPOSITION;
            if (slider_ch7 != null) ui16ServoPos[6] = (UInt16)(slider_ch7.Value); else ui16ServoPos[6] = DEFAULTPOSITION;
            if (slider_ch8 != null) ui16ServoPos[7] = (UInt16)(slider_ch8.Value); else ui16ServoPos[7] = DEFAULTPOSITION;

            for (int i = 8; i < CHANNELCOUNT; i++)
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
                MessageBox.Show("Could not write data out the communication port!" + "\n" + "Error: " + ex.Message);
            }
        }

        private void cbSweep_Checked(object sender, RoutedEventArgs e)
        {
            slider_ch1.Value = DEFAULTPOSITION;
            slider_ch2.Value = DEFAULTPOSITION;
            slider_ch3.Value = DEFAULTPOSITION;
            slider_ch4.Value = DEFAULTPOSITION;
            slider_ch5.Value = DEFAULTPOSITION;
            slider_ch6.Value = DEFAULTPOSITION;
            slider_ch7.Value = DEFAULTPOSITION;
            slider_ch8.Value = DEFAULTPOSITION;

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
                slider_ch1.Value = Math.Min(MAXPOSITION, slider_ch1.Value + SWEEPSTEP);
                slider_ch2.Value = Math.Min(MAXPOSITION, slider_ch2.Value + SWEEPSTEP);
                slider_ch3.Value = Math.Min(MAXPOSITION, slider_ch3.Value + SWEEPSTEP);
                slider_ch4.Value = Math.Min(MAXPOSITION, slider_ch4.Value + SWEEPSTEP);
                slider_ch5.Value = Math.Min(MAXPOSITION, slider_ch5.Value + SWEEPSTEP);
                slider_ch6.Value = Math.Min(MAXPOSITION, slider_ch6.Value + SWEEPSTEP);
                slider_ch7.Value = Math.Min(MAXPOSITION, slider_ch7.Value + SWEEPSTEP);
                slider_ch8.Value = Math.Min(MAXPOSITION, slider_ch8.Value + SWEEPSTEP);

                if (slider_ch1.Value == MAXPOSITION)
                {
                    // Change direction
                    bSweepDirectionUp = false;
                }
            }
            else
            {
                // down
                slider_ch1.Value = Math.Max(MINPOSITION, slider_ch1.Value - SWEEPSTEP);
                slider_ch2.Value = Math.Max(MINPOSITION, slider_ch2.Value - SWEEPSTEP);
                slider_ch3.Value = Math.Max(MINPOSITION, slider_ch3.Value - SWEEPSTEP);
                slider_ch4.Value = Math.Max(MINPOSITION, slider_ch4.Value - SWEEPSTEP);
                slider_ch5.Value = Math.Max(MINPOSITION, slider_ch5.Value - SWEEPSTEP);
                slider_ch6.Value = Math.Max(MINPOSITION, slider_ch6.Value - SWEEPSTEP);
                slider_ch7.Value = Math.Max(MINPOSITION, slider_ch7.Value - SWEEPSTEP);
                slider_ch8.Value = Math.Max(MINPOSITION, slider_ch8.Value - SWEEPSTEP);

                if (slider_ch1.Value == MINPOSITION)
                {
                    // Change direction
                    bSweepDirectionUp = true;
                }
            }
        }

        private void slider_ch1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch1.Value = DEFAULTPOSITION;
        }

        private void slider_ch2_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch2.Value = DEFAULTPOSITION;
        }

        private void slider_ch3_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch3.Value = DEFAULTPOSITION;
        }

        private void slider_ch4_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch4.Value = DEFAULTPOSITION;
        }

        private void slider_ch5_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch5.Value = DEFAULTPOSITION;
        }

        private void slider_ch6_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch6.Value = DEFAULTPOSITION;
        }

        private void slider_ch7_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch7.Value = DEFAULTPOSITION;
        }

        private void slider_ch8_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            slider_ch8.Value = DEFAULTPOSITION;
        }

    }
}
