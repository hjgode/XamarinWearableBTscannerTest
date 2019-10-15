using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;

namespace BTApp1
{
    public class bt_barcode
    {
        public enum eStatus {
            STATE_NONE = 0,       // we're doing nothing
            STATE_LISTEN = 1,     // now listening for incoming connections
            STATE_CONNECTING = 2, // now initiating an outgoing connection
            STATE_CONNECTED = 3,  // now connected to a remote device
        };
        static eStatus m_state = eStatus.STATE_NONE;
        ReadThread readThread;

        public bool Connected = false;
        BluetoothSocket bluetoothSocket;
        Context m_context;
        static string TAG = "bt_barcode";

        public bt_barcode(Context context)
        {
            m_context = context;
            MainActivity.onStatusChanged("");
        }
        public bool Connect(string ScannerModel)//Connect to Paired BT Device containing
        {
            try
            {
                BluetoothAdapter.DefaultAdapter.Enable();
                BluetoothAdapter.DefaultAdapter.CancelDiscovery();
            }
            catch (System.Exception) { }
            ScannerModel = ScannerModel.ToLower();
            foreach (BluetoothDevice device in BluetoothAdapter.DefaultAdapter.BondedDevices)
            {
                Log.Info(TAG, "found: " + device.Name);
                MainActivity.onStatusChanged("found: " + device.Name);
                if (device.Name.ToLower().Contains(ScannerModel))
                {
                    if (device != null && Connected == false)
                    {
                        string SPP_UUID = "00001101-0000-1000-8000-00805f9b34fb";
                        bluetoothSocket = device.CreateInsecureRfcommSocketToServiceRecord(Java.Util.UUID.FromString(SPP_UUID));
                        try
                        {
                            bluetoothSocket.Connect();
                        }
                        catch (Java.Lang.Exception Ex)
                        {
                            Toast.MakeText(m_context, Ex.Message.ToString(), ToastLength.Long).Show();
                            MainActivity.onStatusChanged(Ex.Message);
                            try
                            {
                                System.Threading.Thread.Sleep(1000);
                                bluetoothSocket.Connect();
                            }
                            catch (Java.Lang.Exception Ex2)
                            {
                                Connected = false;
                                Toast.MakeText(m_context, Ex2.Message.ToString(), ToastLength.Long).Show();
                                MainActivity.onStatusChanged(Ex2.Message);
                                m_state = eStatus.STATE_NONE;
                            }
                        }
                        if (bluetoothSocket != null && bluetoothSocket.IsConnected)
                        {
                            Toast.MakeText(m_context, "Bluetooth Scanner Connected", ToastLength.Long).Show();
                            Connected = true;
                            // Start the thread to manage the connection and perform transmissions
                            readThread = new ReadThread(bluetoothSocket);
                            readThread.Start();
                            
                            return true;
                        }
                    }
                    else
                    {
                        Toast.MakeText(m_context, "DEVICE NOT FOUND : " + ScannerModel, ToastLength.Long).Show();
                        Connected = false;
                        m_state = eStatus.STATE_NONE;
                    }
                }
            }
            return false;
        }

        public void write(byte[] buf)
        {
            if(readThread!=null && Connected)
            {
                Log.Debug(TAG, "write: " + util.toHex(Encoding.UTF8.GetString(buf)));
                readThread.Write(buf);
            }
        }

        public void disconnect()
        {
            if (readThread != null && Connected)
            {
                Log.Debug(TAG, "disconnecting");
                MainActivity.onConnectStatusChanged(eStatus.STATE_NONE);
                MainActivity.onStatusChanged("disconnected");
                readThread.Cancel();
            }
        }
        /// <summary>
        /// This thread runs during a connection with a remote device.
        /// It handles all incoming and outgoing transmissions.
        /// </summary>
        private class ReadThread : Thread
        {
            BluetoothSocket m_socket;
            Stream inStream;
            Stream outStream;

            public ReadThread(BluetoothSocket socket)
            {
                m_socket = socket;
                Log.Debug(TAG, $"create ReadThread: {socket}");
                this.m_socket = socket;
                Stream tmpIn = null;
                Stream tmpOut = null;

                // Get the BluetoothSocket input and output streams
                try
                {
                    tmpIn = socket.InputStream;
                    tmpOut = socket.OutputStream;
                }
                catch (Java.IO.IOException e)
                {
                    Log.Error(TAG, "temp sockets not created", e);
                }

                inStream = tmpIn;
                outStream = tmpOut;
                m_state = eStatus.STATE_CONNECTED;
                updateConnectStatus(m_state);
                //this.Run();
            }

            public override void Run()
            {
                Log.Info(TAG, "BEGIN mConnectedThread");
                byte[] buffer = new byte[1024];
                int bytes;

                // Keep listening to the InputStream while connected
                while (m_state == eStatus.STATE_CONNECTED)
                {
                    try
                    {
                        buffer = new byte[1024];
                        // Read from the InputStream
                        bytes = inStream.Read(buffer, 0, buffer.Length);

                        string s = System.Text.Encoding.ASCII.GetString(buffer);
                        Log.Debug(TAG, "Data read: " + util.toHex(s));
                        if (s.Contains("MSGGET"))
                        {
                            util.BarcodeData myBarcodeData = util.getBarcodeData(buffer);
                            //Log.Info(TAG, "received: " + util.toHex(s));
                            Log.Info(TAG, "Barcode Data: " + util.toHex(myBarcodeData.sData + ", AimID=" + myBarcodeData.AimID + myBarcodeData.AimMod));

                            // Send the obtained bytes to the UI Activity
                            //updateStatus(util.toHex(s));

                            updateStatus("Barcode data: " + util.toHex(myBarcodeData.sData) + ", AimID=" + myBarcodeData.AimID + myBarcodeData.AimMod);
                        }else if (util.sIsACK(s)!="")
                        {
                            updateStatus("ACK: " + util.sIsACK(s));
                            Log.Debug(TAG, "ACK: " + util.sIsACK(s));
                        }else if (util.sIsDataOnly(s) != "")
                        {
                            updateStatus("Data: " + util.sIsDataOnly(s));
                            Log.Debug(TAG, "Data: " + util.sIsDataOnly(s));
                        }
                    }
                    catch (Java.IO.IOException e)
                    {
                        Log.Error(TAG, "disconnected", e);
                        m_state = eStatus.STATE_NONE;
//                        service.ConnectionLost();
                        break;
                    }
                }
                Log.Info(TAG, "END mConnectedThread");
            }

            /// <summary>
            /// Write to the connected OutStream.
            /// </summary>
            /// <param name='buffer'>
            /// The bytes to write
            /// </param>
            public void Write(byte[] buffer)
            {
                try
                {
                    Log.Debug(TAG, "readThread write: " + util.toHex(Encoding.UTF8.GetString(buffer)));
                    outStream.Write(buffer, 0, buffer.Length);
                    
                    // Share the sent message back to the UI Activity
                    //service.handler
                    //       .ObtainMessage(Constants.MESSAGE_WRITE, -1, -1, buffer)
                    //       .SendToTarget();
                }
                catch (Java.IO.IOException e)
                {
                    Log.Error(TAG, "Exception during write", e);
                }
            }

            /*
            Menu commands have the following syntax (spaces have been used for clarity
            only): Prefix Tag SubTag {Data} [, SubTag {Data}] [; Tag SubTag {Data}] […] Storage
            Prefix
                Three ASCII characters: SYN M CR (ASCII 22,77,13).
            Tag 
                A 3 character case-insensitive field that identifies the desired menu command group.
            SubTag
                A 3 character case-insensitive field that identifies the desired menu command within the tag group.
            Data 
                The new value for a menu setting, identified by the Tag and SubTag.
            Storage 
                A single character that specifies the storage table to which the command
                is applied. An exclamation point (!) performs the command’s
                operation on the device’s volatile menu configuration table. A period
                (.) performs the command’s operation on the device’s non-volatile
                menu configuration table. Use the non-volatile table only for semipermanent
                changes you want saved through a power cycle.
            */
            public void Cancel()
            {
                try
                {
                    m_state = eStatus.STATE_NONE;
                    m_socket.Close();
                    this.Interrupt();
                }
                catch (Java.IO.IOException e)
                {
                    Android.Util.Log.Error(TAG, "close() of connect socket failed", e);
                }
            }
            void updateStatus(string msg)
            {
                Application.SynchronizationContext.Post(_ => {
                    /* invoked on UI thread */
                    MainActivity.txtLog.Text += msg + "\r\n";
                }, null);
            }

            void updateConnectStatus(eStatus iState)
            {
                Application.SynchronizationContext.Post(_ => {
                    /* invoked on UI thread */
                    MainActivity.onConnectStatusChanged(iState);
                }, null);
            }
        }
    }

    public class util
    {
        public static string toHex(string sIN)
        {
            if (sIN == null)
                return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            char[] cArr = sIN.ToCharArray();
            foreach(char c in cArr)
            {
                if(c < ' ')
                {
                    sb.Append( "<" + ((byte)c).ToString("x") +">" );
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /*  Without MsgHeader:
         *  10110<d><0><0><0
         *  
         *  With MSGHDR:
         *  <16>?$<0><0><0><d>MSGGET0022IC1<1d>1012345678<1d>2187654321<d><0><0>
         *  <1d>1012345678<1d>2187654321<d>
         *  <16>?
         *      $<0><0><0>
         *          <d>
         *              MSGGET
         *                  0022    len as string
         *                      IC1<1d>1012345678<1d>2187654321<d><0><0>
         *  [SYN]?$[NUL][NUL][NUL][CR]MSGGET0022
         *  hex 22 => dec 34
         *  
         *  7 character SYN FE header, 
              where byte 0 is a <SYN> (0x16) character, 
              byte 1 is a 0xFE character, 
              byte 6 is a <CR> (0x0D) character, and bytes 2 to 5 are the little 
              endian length of the MSGGET command and data following this header.
            6 character MSGGET command.
            4 character decimal length of the barcode data. For example, 0015 indicates a bar code data length of 15 decimal.
            1 character Honeywell Code ID
            1 character AIM ID
            1 character AIM modifier
            <GS> (0x1D) character to mark the end of the command / parameters and the beginning of data.
            Actual bar code data.
        */
        public static BarcodeData getBarcodeData(byte[] bData)
        {
            BarcodeData bcData = new BarcodeData("   ");
            if (bData[0]==0x16 && bData[1] == 0xFE && bData[6]==0x0d)
            {
                byte[] bLen = new byte[4];
                Array.ConstrainedCopy(bData, 2, bLen, 0, 4);
                UInt32 uL = BitConverter.ToUInt32(bLen);
                byte[] bDataMSG = new byte[uL];
                Array.ConstrainedCopy(bData, 7, bDataMSG, 0, (int)uL);
                //=> MSGGET0022IC1<1d>1012345678<1d>2187654321<d><0><0>
                string sMSG = Encoding.UTF8.GetString(bDataMSG);
                //now we get the message length
                int iMsgLen = int.Parse(sMSG.Substring(6, 4));// look at MSGGET0022...
                bcData = new BarcodeData(sMSG.Substring(10, 3));
                bcData.sData = sMSG.Substring(13, iMsgLen);

            }
            return bcData;
        }

        public static string sIsACK(string sIN)
        {
            int iPosAck = sIN.IndexOf("\x06");
            if (iPosAck>=0)
            {
                return sIN.Substring(0, iPosAck);
            }
            return "";
        }

        public static string sIsDataOnly(string sIN)
        {
            int iPosCR = sIN.IndexOf("\x0D");
            if (iPosCR >= 0)
            {
                return sIN.Substring(0, iPosCR);
            }
            return "";

        }
        public class BarcodeData
        {
            public string honID { get; set; }
            public string AimID { get; set; }
            public string AimMod { get; set; }
            public string sData { get; set; }
            public BarcodeData(string sIDs)
            {
                honID = sIDs.Substring(0, 1);
                AimID = sIDs.Substring(1, 1);
                AimMod = sIDs.Substring(2, 1);
            }
            public override string ToString()
            {
                return sData;
            }
        }

        public static byte[] setTriggerOnMsg()
        {
            // SYN T CR
            return Encoding.UTF8.GetBytes("\x16T\x0d");
        }
        public static byte[] setTriggerOffMsg()
        {
            // SYN U CR
            return Encoding.UTF8.GetBytes("\x16U\x0d");
        }
        public static byte[] setManualTriggerMode()
        {
            string s = "PAPHHF.";
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] setTriggerClickOn()
        {
            string s = "BEPTRG1.";
            return Encoding.UTF8.GetBytes(s);
        }
        public static byte[] setTriggerClickOff()
        {
            string s = "BEPTRG0.";
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] setGoodReadBeepOff()
        {
            string s = SynMenuCommandHeader + "BEPBEP0" + SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] setGoodReadBeepOn()
        {
            string s = SynMenuCommandHeader+"BEPBEP1"+SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] getDecHeaderSetting()
        {
            string s = SynMenuCommandHeader + "DECHDR?" + SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] setDecHeaderSetting(bool bOnOff)
        {
            string s = SynMenuCommandHeader + "DECHDR" + (bOnOff ? "1" : "0") + SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] setDoBeep()
        {
            string s = SynMenuCommandHeader+"BEPEXE1"+SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] SetAllSymbologiesOnOff(bool bOnOff)
        {
            string s = SynMenuCommandHeader + "ALLENA" + (bOnOff ? "1" : "0") + SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        static string deviceName = ""; //or ":*:" or BT friendly name, ie ":8680i wearable:
        public static string SynMenuCommandHeader = "\x16M\x0d" + deviceName; //:*: = name of device
        public static string SynYCommandHeader = "\x16Y\x0d" + deviceName; //:*: = name of device
        public static string SynMenuCommandSuffixFlash = ".";
        public static string SynMenuCommandSuffixRAM = "!";
        public enum eASCII_codes
        {
            NUL=0,
            ACK=0x06,
            CR=0x13,
            NAK=0x15,
            SYN=0x16,
            GS=0x1D,
        }
    }
}