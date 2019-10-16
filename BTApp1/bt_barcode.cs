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
                int bytesReadCount;

                // Keep listening to the InputStream while connected
                while (m_state == eStatus.STATE_CONNECTED)
                {
                    try
                    {
                        buffer = new byte[1024];
                        // Read from the InputStream
                        bytesReadCount = inStream.Read(buffer, 0, buffer.Length);
                        byte[] readBytes = new byte[bytesReadCount];
                        Array.Copy(buffer, readBytes, bytesReadCount);
                        
                        string s = System.Text.Encoding.ASCII.GetString(readBytes);
                        Log.Debug(TAG, "Data read: " + util.toHex(s));
                        if (s.Contains("MSGGET"))
                        {
                            util.BarcodeData myBarcodeData = util.getBarcodeData(readBytes);
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

    }